using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Diagnostics;

namespace Ceremony.Models
{
    public class GenericRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        internal CeremonyEntities context;
        internal DbSet<TEntity> dbSet;

        public GenericRepository() : this(new CeremonyEntities())
        {
        }

        public GenericRepository(CeremonyEntities context)
        {
            this.context = context;
            this.dbSet = context.Set<TEntity>();
        }

        public IQueryable<TEntity> Get()
        {
            IQueryable<TEntity> query = dbSet;

            return query;
        }

        public TEntity GetByID(object id)
        {
            TEntity entity = dbSet.Find(id);

            return entity;
        }

        public void Insert(TEntity entity)
        {
            dbSet.Add(entity);
        }

        public void Delete(object id)
        {
            TEntity entityToDelete = dbSet.Find(id);
            Delete(entityToDelete);
        }

        public void Delete(TEntity entityToDelete)
        {
            //若沒有被track
            if (context.Entry(entityToDelete).State == EntityState.Detached)
            {
                dbSet.Attach(entityToDelete);
            }
            dbSet.Remove(entityToDelete);
        }

        public void Delete(IEnumerable<TEntity> entitiesToDelete)
        {
            dbSet.RemoveRange(entitiesToDelete);
        }

        public void Update(TEntity entityToUpdate)
        {
            dbSet.Attach(entityToUpdate);
            context.Entry(entityToUpdate).State = EntityState.Modified;
        }

        public void SpecificUpdate(TEntity entityToUpdate, string[] Includeproperties)
        {
            //關閉欄位驗證
            context.Configuration.ValidateOnSaveEnabled = false;
            dbSet.Attach(entityToUpdate);
            foreach (string property in Includeproperties)
            {
                context.Entry(entityToUpdate).Property(property).IsModified = true;
            }
        }

        public void SaveChanges()
        {
            context.SaveChanges();
        }

        public void SwitchLazyLoading(bool isenable)
        {
            context.Configuration.LazyLoadingEnabled = isenable;
        }

        public void ExeLog()
        {
            context.Database.Log = log => Debug.Write(log);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (context != null)
                {
                    context.Dispose();
                    context = null;
                }
            }
        }
    }
}
