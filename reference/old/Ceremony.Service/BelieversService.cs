using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class BelieversService : BaseService<Believers>
    {
        public BelieversService()
        {
            repository = new GenericRepository<Believers>();
        }

        public BelieversService(CeremonyEntities context)
        {
            repository = new GenericRepository<Believers>(context);
        }
    }
}
