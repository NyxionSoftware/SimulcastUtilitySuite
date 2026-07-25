using SimulcastUtility.Handlers;
using SimulcastUtility.Shared.Commands;
using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Documents;

namespace SimulcastUtility.Services
{
    public sealed class ReceiverDiscoveryService
    {
        public async Task<ReceiverDiscoveryResult> DiscoverAsync(Receiver receiver, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);
            receiver.LastError = null;

            try
            {
                var command = new HELLO_DISCOVERY();

                CommandResult<HELLO_DISCOVERY_RESPONSE> result = await CommandHandler.SendCommandAsync<HELLO_DISCOVERY_RESPONSE>(receiver, command, TimeSpan.FromSeconds(6), cancellationToken);

                if (!result.IsSuccess || result.Response is null)
                {
                    receiver.Status = ReceiverStatus.Offline;
                    receiver.LastError = result.ErrorMessage ?? "Receiver discovery failed.";

                    return new ReceiverDiscoveryResult
                    {
                        DiscoveryResult = result,
                        EpgLoadTask = Task.CompletedTask
                    };
                }

                HELLO_DISCOVERY_RESPONSE hello = result.Response;

                if (!string.Equals(hello.StbChipID, receiver.ReceiverId, StringComparison.Ordinal))
                {
                    receiver.Status = ReceiverStatus.Offline;

                    receiver.LastError = $"The receiver at {receiver.IpAddress} reported ID '{hello.StbChipID}', but '{receiver.ReceiverId}' was entered.";

                    return new ReceiverDiscoveryResult
                    {
                        DiscoveryResult = CommandResult<HELLO_DISCOVERY_RESPONSE>.Failure(receiver.LastError),
                        EpgLoadTask = Task.CompletedTask
                    };
                }

                ApplyHelloResponse(receiver, hello);

                receiver.Status = ReceiverStatus.Online;
                receiver.LastError = null;

                Task epgLoadTask = LoadCurrentEpgAsync(receiver, cancellationToken);

                return new ReceiverDiscoveryResult
                {
                    DiscoveryResult = result,
                    EpgLoadTask = epgLoadTask
                };
            }
            finally
            {

            }
        }

        private static async Task LoadCurrentEpgAsync(Receiver receiver, CancellationToken cancellationToken = default)
        {
            var epgListCommand = new CMD_GET_LIST_EPG();

            CommandResult<CMD_STB_MESSAGE_RESPONSE<List<CMD_GET_LIST_EPG_RESPONSE>>> epgListResult = 
                await CommandHandler.SendCommandAsync<CMD_STB_MESSAGE_RESPONSE<List<CMD_GET_LIST_EPG_RESPONSE>>>(
                        receiver,
                        epgListCommand, TimeSpan.FromSeconds(5),
                        cancellationToken);

            if (!epgListResult.IsSuccess || epgListResult.Response?.Details is null || epgListResult.Response == null)
            {
                ClearCurrentEpg(receiver);
                return;
            }

            var command = new CMD_GET_CURRENT_EPG(receiver.IsNewVersion, epgListResult.Response.Details[0].ServiceId);

            CommandResult<CMD_STB_MESSAGE_RESPONSE<CMD_GET_CURRENT_EPG_RESPONSE>> result = 
                await CommandHandler.SendCommandAsync<CMD_STB_MESSAGE_RESPONSE<CMD_GET_CURRENT_EPG_RESPONSE>>(
                        receiver,
                        command, TimeSpan.FromSeconds(30),
                        cancellationToken);

            if (!result.IsSuccess || result.Response?.Details is null)
            {
                ClearCurrentEpg(receiver);
                return;
            }

            CMD_GET_CURRENT_EPG_RESPONSE epg = result.Response.Details;

            receiver.Channel = epg.ServiceId;

            receiver.ChannelName = epg.ChannelName;

            receiver.ChannelTitle = epg.Title;

            if(epg.StartTime.HasValue)
                receiver.ChannelStartTime = epg.StartTime.Value;

            receiver.ChannelDuration = epg.Duration;

            receiver.ChannelRemainingTime = epg.DurationLeft;

            if (epg.EndTime.HasValue)
                receiver.ChannelEndTime = epg.EndTime.Value;
        }

        private static void ClearCurrentEpg(Receiver receiver)
        {
            receiver.Channel = 0;

            receiver.ChannelName = string.Empty;

            receiver.ChannelTitle = string.Empty;

            receiver.ChannelStartTime = null;

            receiver.ChannelEndTime = null;

            receiver.ChannelDuration = null;

            receiver.ChannelRemainingTime = null;
        }

        private static void ApplyHelloResponse(Receiver receiver, HELLO_DISCOVERY_RESPONSE hello)
        {
            receiver.ApkVersion = hello.ApkVersion;

            receiver.TunerSoftwareVersion = hello.TunerSWInfo;

            receiver.TunerSoftwareBuildInfo = hello.TunerSWBuildInfo;

            receiver.EthernetMac = FormatMacAddress(hello.EthernetMac);

            receiver.DeviceInfo = hello.DeviceInfo;
        }

        private static string FormatMacAddress(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                return string.Empty;

            string normalized = new string(mac.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

            if (normalized.Length != 12)
                return mac;

            return string.Join(":", Enumerable.Range(0, 6).Select(index => normalized.Substring(index * 2, 2)));
        }
    }
}
