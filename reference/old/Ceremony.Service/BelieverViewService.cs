using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class BelieverViewService : BaseService<BelieverView>
    {
        public BelieverViewService()
        {
            repository = new GenericRepository<BelieverView>();
        }

        public BelieverViewService(CeremonyEntities context)
        {
            repository = new GenericRepository<BelieverView>(context);
        }
    }
}
