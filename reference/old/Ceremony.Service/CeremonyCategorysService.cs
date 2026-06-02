using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class CeremonyCategorysService : BaseService<CeremonyCategorys>
    {
        public CeremonyCategorysService()
        {
            repository = new GenericRepository<CeremonyCategorys>();
        }

        public CeremonyCategorysService(CeremonyEntities context)
        {
            repository = new GenericRepository<CeremonyCategorys>(context);
        }
    }
}
