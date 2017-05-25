using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LastPassImporter
{
    struct PasswordEntry
    {
        public string Title { get; set; }
        public Uri Url { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Note { get; set; }
        public string Group { get; set; }
        public bool Favorite { get; set; }
    }
}
