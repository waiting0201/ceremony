using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class SignupsService : BaseService<Signups>
    {
        public SignupsService()
        {
            repository = new GenericRepository<Signups>();
        }

        public SignupsService(CeremonyEntities context)
        {
            repository = new GenericRepository<Signups>(context);
        }
    }
}
