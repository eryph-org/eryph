using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class IpAssignmentConfigModel
    {
        public string IpAddress { get; set; }

        public string SubnetName { get; set; }

        /// <summary>
        /// The name of the IP pool. Can be <see langword="null"/> when the IP
        /// of the assignment was not taken from a pool.
        /// </summary>
        public string PoolName { get; set; }
    }
}
