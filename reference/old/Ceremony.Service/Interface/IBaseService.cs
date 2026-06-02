using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceremony.Service
{
    public interface IBaseService<TEntity> : IDisposable where TEntity : class
    {
        IResult Create(TEntity entity);
        IResult Update(TEntity entity);
        IResult SpecificUpdate(TEntity entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(TEntity entity);
        IResult Delete(IEnumerable<TEntity> entities);
        TEntity GetByID(object id);
        IQueryable<TEntity> Get();
        void SaveChanges();
        void SwitchLazyLoading(bool isenable);
        void ExeLog();
    }
}
