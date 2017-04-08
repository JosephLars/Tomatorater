using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tomatorater
{

    class Movie
    {
        public string Title { get; set; }
        public int Year { get; set; }

        public int MeterScore { get; set; }
        public string MeterClass { get; set; } //"certified_fresh", "fresh", "rotten", or "N/A"

        private int audienceScore;
        public int AudienceScore
        {
            get
            {
                return this.audienceScore;
            }
            set
            {
                this.audienceScore = value;

                if (AudienceClass != "want")
                    if (value >= 60)
                        AudienceClass = "upright";
                    else
                        AudienceClass = "spilled";
            }
        }
        public string AudienceClass { get; set; } //"upright", "spilled", "want", or "N/A"

        public string Url { get; set; }

        public string Listing
        {
            get { return Title + " (" + Year + ")"; }
        }

        public Movie()
        {

        }

        public Movie(string title, int year, string url, int meterScore, string meterClass)
        {
            this.Title = title;
            this.Year = year;
            this.Url = url;
            this.MeterScore = meterScore;
            this.MeterClass = meterClass;
        }
    }
}
