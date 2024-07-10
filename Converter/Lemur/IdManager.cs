namespace Converter.Lemur
{
    public class IdManager
    {
        private static IdManager? instance;
        private int currentId;

        private IdManager()
        {
            currentId = 0;
        }

        public static IdManager Instance
        {
            get
            {
                instance ??= new IdManager();
                return instance;
            }
        }

        public int GetNextId()
        {
            return currentId++;
        }
    }
}