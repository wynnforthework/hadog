using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDKTemplate
{
    class DeviceEntity
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public bool IsAtHome { get; set; } = true;

        public DeviceEntity(string id,string name)
        {
            this.Id = id;
            this.Name = name;
        }

        public DeviceEntity(string device)
        {
            var data = device.Split(",");
            if (data.Length == 2)
            {
                this.Name = data[0];
                this.Id = data[1];
            }
        }

        public override string ToString()
        {
            return $"{Name},{Id}";
        }
    }
}
