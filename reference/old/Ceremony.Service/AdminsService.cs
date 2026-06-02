using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class AdminsService : BaseService<Admins>
    {
        public AdminsService()
        {
            repository = new GenericRepository<Admins>();
        }

        public AdminsService(CeremonyEntities context)
        {
            repository = new GenericRepository<Admins>(context);
        }
    }
}
