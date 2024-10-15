using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentManagerPOC.Models
{
    public class UserAuthorizationModel
    {
        public string username { get; set; }
        public string password { get; set; }
        public string locationid { get; set; }
    }
}
