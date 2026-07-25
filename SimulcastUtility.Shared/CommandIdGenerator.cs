namespace SimulcastUtility.Shared
{
    public class CommandIdGenerator
    {
        private static int _currentId = Random.Shared.Next(1, int.MaxValue / 2);

        public static int Next()
        {
            int id = Interlocked.Increment(ref _currentId);

            if (id <= 0)
            {
                Interlocked.Exchange(ref _currentId, 1);
                return 1;
            }

            return id;
        }
    }
}
