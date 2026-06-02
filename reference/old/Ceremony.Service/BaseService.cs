using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ceremony.Models;

namespace Ceremony.Service
{
    public class BaseService<TEntity> : IBaseService<TEntity> where TEntity : class
    {
        public IRepository<TEntity> repository;

        public BaseService()
        {
            repository = new GenericRepository<TEntity>();
        }

        public BaseService(CeremonyEntities context)
        {
            repository = new GenericRepository<TEntity>(context);
        }

        public IResult Create(TEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException();
            }

            IResult result = new Result(false);
            try
            {
                this.repository.Insert(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }

        public IResult Update(TEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException();
            }

            IResult result = new Result(false);
            try
            {
                this.repository.Update(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }

        public IResult SpecificUpdate(TEntity entity, string[] Includeproperties)
        {
            if (entity == null)
            {
                throw new ArgumentNullException();
            }

            IResult result = new Result(false);
            try
            {
                this.repository.SpecificUpdate(entity, Includeproperties);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }

        public IResult Delete(object id)
        {
            IResult result = new Result(false);

            var entity = this.GetByID(id);

            if (entity == null)
            {
                result.Message = "找不到資料";
            }

            try
            {
                this.repository.Delete(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }

        public IResult Delete(TEntity entity)
        {
            IResult result = new Result(false);

            if (entity == null)
            {
                result.Message = "找不到資料";
            }

            try
            {
                this.repository.Delete(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }

        public IResult Delete(IEnumerable<TEntity> entities)
        {
            IResult result = new Result(false);

            if (entities == null)
            {
                result.Message = "找不到資料";
            }

            try
            {
                this.repository.Delete(entities);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }

        public TEntity GetByID(object id)
        {
            return this.repository.GetByID(id);
        }

        public IQueryable<TEntity> Get()
        {
            return this.repository.Get();
        }

        public void SaveChanges()
        {
            this.repository.SaveChanges();
        }

        public void SwitchLazyLoading(bool isenable)
        {
            this.repository.SwitchLazyLoading(isenable);
        }

        public void ExeLog()
        {
            this.repository.ExeLog();
        }

        public void Dispose()
        {
            this.Dispose();
        }
    }
}
