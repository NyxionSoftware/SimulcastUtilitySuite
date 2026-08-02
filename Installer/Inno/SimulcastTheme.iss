const
  SimulcastWindowColor = $171210;
  SimulcastSurfaceColor = $251D19;
  SimulcastRaisedColor = $322823;
  SimulcastBorderColor = $3C302A;
  SimulcastTextColor = $FAF6F4;
  SimulcastMutedTextColor = $BCAFA8;
  SimulcastAccentColor = $FC5C7C;
  DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
  PBM_SETBARCOLOR = $0409;
  PBM_SETBKCOLOR = $2001;

var
  RemovePluginDataOnUninstall: Boolean;
  RemoveReceiversOnUninstall: Boolean;

function DwmSetWindowAttribute(hWnd: HWND; dwAttribute: LongWord; var pvAttribute: Integer; cbAttribute: LongWord): Integer; external 'DwmSetWindowAttribute@dwmapi.dll stdcall delayload';

procedure ApplyDarkTitleBar(const WindowHandle: HWND);
var
  Enabled: Integer;
begin
  Enabled := 1;
  DwmSetWindowAttribute(WindowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE, Enabled, SizeOf(Enabled));
end;

procedure ApplySimulcastTheme;
begin
  WizardForm.Color := SimulcastWindowColor;
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.Font.Color := SimulcastTextColor;
  WizardForm.MainPanel.Color := SimulcastSurfaceColor;
  WizardForm.WelcomePage.Color := SimulcastWindowColor;
  WizardForm.InnerPage.Color := SimulcastWindowColor;
  WizardForm.InstallingPage.Color := SimulcastWindowColor;
  WizardForm.FinishedPage.Color := SimulcastWindowColor;
  WizardForm.PageNameLabel.Font.Color := SimulcastTextColor;
  WizardForm.PageDescriptionLabel.Font.Color := SimulcastMutedTextColor;
  WizardForm.WelcomeLabel1.Font.Color := SimulcastTextColor;
  WizardForm.WelcomeLabel2.Font.Color := SimulcastMutedTextColor;
  WizardForm.FinishedHeadingLabel.Font.Color := SimulcastTextColor;
  WizardForm.FinishedLabel.Font.Color := SimulcastMutedTextColor;
  WizardForm.StatusLabel.Font.Color := SimulcastTextColor;
  WizardForm.FilenameLabel.Font.Color := SimulcastMutedTextColor;
  WizardForm.ReadyMemo.Color := SimulcastRaisedColor;
  WizardForm.ReadyMemo.Font.Color := SimulcastTextColor;
  SendMessage(WizardForm.ProgressGauge.Handle, PBM_SETBARCOLOR, 0, SimulcastAccentColor);
  SendMessage(WizardForm.ProgressGauge.Handle, PBM_SETBKCOLOR, 0, SimulcastRaisedColor);
  ApplyDarkTitleBar(WizardForm.Handle);
end;

procedure ApplySimulcastUninstallTheme;
begin
  UninstallProgressForm.Color := SimulcastWindowColor;
  UninstallProgressForm.Font.Name := 'Segoe UI';
  UninstallProgressForm.Font.Color := SimulcastTextColor;
  UninstallProgressForm.MainPanel.Color := SimulcastSurfaceColor;
  UninstallProgressForm.InnerPage.Color := SimulcastWindowColor;
  UninstallProgressForm.InstallingPage.Color := SimulcastWindowColor;
  UninstallProgressForm.PageNameLabel.Font.Color := SimulcastTextColor;
  UninstallProgressForm.PageDescriptionLabel.Font.Color := SimulcastMutedTextColor;
  UninstallProgressForm.StatusLabel.Font.Color := SimulcastTextColor;
  SendMessage(UninstallProgressForm.ProgressBar.Handle, PBM_SETBARCOLOR, 0, SimulcastAccentColor);
  SendMessage(UninstallProgressForm.ProgressBar.Handle, PBM_SETBKCOLOR, 0, SimulcastRaisedColor);
  ApplyDarkTitleBar(UninstallProgressForm.Handle);
end;

function ShowUninstallDataOptions: Boolean;
var
  CancelButton: TNewButton;
  ContinueButton: TNewButton;
  DataOptionsList: TNewCheckListBox;
  DescriptionLabel: TNewStaticText;
  Form: TSetupForm;
  TitleLabel: TNewStaticText;
begin
  Form := CreateCustomForm();

  try
    Form.ClientWidth := ScaleX(520);
    Form.ClientHeight := ScaleY(230);
    Form.Caption := 'Uninstall Simulcast Utility';
    Form.Color := SimulcastWindowColor;
    Form.Font.Name := 'Segoe UI';
    Form.Font.Color := SimulcastTextColor;
    Form.BorderStyle := bsDialog;

    TitleLabel := TNewStaticText.Create(Form);
    TitleLabel.Parent := Form;
    TitleLabel.Left := ScaleX(24);
    TitleLabel.Top := ScaleY(22);
    TitleLabel.Width := Form.ClientWidth - ScaleX(48);
    TitleLabel.Caption := 'Choose application data to remove';
    TitleLabel.Font.Color := SimulcastTextColor;
    TitleLabel.Font.Style := [fsBold];

    DescriptionLabel := TNewStaticText.Create(Form);
    DescriptionLabel.Parent := Form;
    DescriptionLabel.Left := ScaleX(24);
    DescriptionLabel.Top := ScaleY(50);
    DescriptionLabel.Width := Form.ClientWidth - ScaleX(48);
    DescriptionLabel.Height := ScaleY(38);
    DescriptionLabel.AutoSize := False;
    DescriptionLabel.WordWrap := True;
    DescriptionLabel.Caption := 'Application files will be removed. Select any additional data belonging to the current Windows account that should also be deleted.';
    DescriptionLabel.Font.Color := SimulcastMutedTextColor;

    DataOptionsList := TNewCheckListBox.Create(Form);
    DataOptionsList.Parent := Form;
    DataOptionsList.Left := ScaleX(24);
    DataOptionsList.Top := ScaleY(96);
    DataOptionsList.Width := Form.ClientWidth - ScaleX(48);
    DataOptionsList.Height := ScaleY(70);
    DataOptionsList.Flat := True;
    DataOptionsList.Color := SimulcastWindowColor;
    DataOptionsList.Font.Color := SimulcastTextColor;
    DataOptionsList.AddCheckBox('Remove installed plugins and plugin data', '', 0, False, True, False, True, nil);
    DataOptionsList.AddCheckBox('Remove configured receivers', '', 0, False, True, False, True, nil);

    ContinueButton := TNewButton.Create(Form);
    ContinueButton.Parent := Form;
    ContinueButton.Caption := 'Continue';
    ContinueButton.Left := Form.ClientWidth - ScaleX(178);
    ContinueButton.Top := Form.ClientHeight - ScaleY(42);
    ContinueButton.Width := ScaleX(78);
    ContinueButton.Height := ScaleY(25);
    ContinueButton.ModalResult := mrOk;
    ContinueButton.Default := True;

    CancelButton := TNewButton.Create(Form);
    CancelButton.Parent := Form;
    CancelButton.Caption := 'Cancel';
    CancelButton.Left := Form.ClientWidth - ScaleX(92);
    CancelButton.Top := ContinueButton.Top;
    CancelButton.Width := ScaleX(68);
    CancelButton.Height := ScaleY(25);
    CancelButton.ModalResult := mrCancel;
    CancelButton.Cancel := True;

    Form.ActiveControl := ContinueButton;
    Form.KeepSizeY := True;
    ApplyDarkTitleBar(Form.Handle);
    Result := Form.ShowModal() = mrOk;

    if Result then
    begin
      RemovePluginDataOnUninstall := DataOptionsList.Checked[0];
      RemoveReceiversOnUninstall := DataOptionsList.Checked[1];
    end;
  finally
    Form.Free();
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := ShowUninstallDataOptions;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDirectory: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  DataDirectory := ExpandConstant('{localappdata}\SimulcastUtility');

  if RemovePluginDataOnUninstall then
  begin
    DelTree(DataDirectory + '\Plugins', True, True, True);
    DelTree(DataDirectory + '\PluginData', True, True, True);
  end;

  if RemoveReceiversOnUninstall then
    DeleteFile(DataDirectory + '\receivers.json');

  RemoveDir(DataDirectory);
end;

procedure InitializeUninstallProgressForm;
begin
  ApplySimulcastUninstallTheme;
end;
