using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceremony.Models
{
    public interface IRepository<TEntity> : IDisposable where TEntity : class
    {
        void Insert(TEntity entity);
        void Update(TEntity entity);
        void SpecificUpdate(TEntity entity, string[] Includeproperties);
        void Delete(object id);
        void Delete(TEntity entity);
        void Delete(IEnumerable<TEntity> entities);
        TEntity GetByID(object id);
        IQueryable<TEntity> Get();
        void SaveChanges();
        void SwitchLazyLoading(bool isenable);
        void ExeLog();
    }
}
