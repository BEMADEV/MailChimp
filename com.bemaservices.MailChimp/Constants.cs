using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.bemaservices.MailChimp
{
    public static class Constants
    {

        public static string ForeignKey = "com.bemaservices.MailChimp";

        public static List<string> MERGE_TAGS_TO_IGNORE = new List<string> { "FNAME", "LNAME", "ADDRESS" };

    }
}
