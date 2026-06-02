using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceremony
{
    public static class Global
    {
        private static bool _islogin;
        private static string _username;
        private static int _adminid;
        private static string _apptitle = "法會報名系統";
        private static string _version = "v1.2.8";

        public static bool Islogin
        {
            get { return _islogin; }
            set
            {
                _islogin = value;
            }
        }

        public static string Username
        {
            get { return _username; }
            set
            {
                _username = value;
            }
        }

        public static int AdminID
        {
            get { return _adminid; }
            set
            {
                _adminid = value;
            }
        }

        public static string AppTitle
        {
            get { return _apptitle; }
            set
            {
                _apptitle = value;
            }
        }

        public static string Version
        {
            get { return _version; }
            set
            {
                _version = value;
            }
        }
    }
}
