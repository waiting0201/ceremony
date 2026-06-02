using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class SignupViewService : BaseService<SignupView>
    {
        public SignupViewService()
        {
            repository = new GenericRepository<SignupView>();
        }

        public SignupViewService(CeremonyEntities context)
        {
            repository = new GenericRepository<SignupView>(context);
        }
    }
}
