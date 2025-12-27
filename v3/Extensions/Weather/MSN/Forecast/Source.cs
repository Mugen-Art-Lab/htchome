namespace MSN.Forecast
{
    public class Source
    {
        public Coordinates Coordinates { get; set; }

        public Location Location { get; set; }

        public Source()
        {
            Coordinates = new Coordinates();
        }
    }
}
