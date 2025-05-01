using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;

namespace OPCUaClient.old.Objects
{

    /// <summary>
    /// Representation in class of a the tag of OPC UA Server
    /// </summary>
    public class Tag
    {

        /// <summary>
        /// Name of the tag
        /// </summary>
        public string Name
        {
            get
            {
                return Address.Substring(Address.LastIndexOf(".") + 1);
            }
        }

        /// <summary>
        /// Address of the tag
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Value of the tag
        /// </summary>
        public object Value { get; set; }


        /// <summary>
        /// Status code of the tag
        /// </summary>
        public StatusCode Code { get; set; }

        /// <summary>
        /// Quality of the tag
        /// </summary>
        public bool Quality { 
            get 
            {
                return StatusCode.IsGood(Code);
            } 
        }
    }
}
