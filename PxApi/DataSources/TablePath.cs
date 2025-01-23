namespace PxApi.DataSources
{
    public class TablePath : List<string>
    {
        public TablePath(string path)
        {
            foreach (string item in path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                Add(item);
            }
        }

        public string ToPathString()
        {
            return Path.Combine([.. this]);
        }
    }
}
