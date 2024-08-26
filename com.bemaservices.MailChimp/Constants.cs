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

        public static string ROOT_API_URL = "https://{0}.api.mailchimp.com/3.0/";

        public static string LIST_MEMBER_TAGS = "lists/{0}/members/{1}/tags";

    }
}
