using System;
using System.Threading;
using System.IO.Ports;
using System.Collections.Generic;
using System.Text;

namespace Curit.Module.RTX.Com
{
    public class ComListener
    {
        private SerialPort sp;

        public Boolean update { get; private set; }
        public Boolean binary { get; set; }

        public void Start(SerialPort lsp)
        {

        }

        
    }
}
