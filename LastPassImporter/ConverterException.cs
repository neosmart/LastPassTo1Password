using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LastPassImporter
{
    class ConverterException : System.Exception
    {
        public string Title { get; set; }

        public ConverterException(string title, string message = "", Exception innerException = null)
            : base(message, innerException)
        {
            Title = title;
        }
    }
}
