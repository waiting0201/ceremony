using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class SignupLogsService : BaseService<SignupLogs>
    {
        public SignupLogsService()
        {
            repository = new GenericRepository<SignupLogs>();
        }

        public SignupLogsService(CeremonyEntities context)
        {
            repository = new GenericRepository<SignupLogs>(context);
        }
    }
}
