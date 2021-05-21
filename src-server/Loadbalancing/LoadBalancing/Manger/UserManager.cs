using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Collection;
using NHibernate.Criterion;
using Photon.LoadBalancing.Model;


namespace Photon.LoadBalancing.Manger
{
    class UserManager : IUserManager
    {
        public void Add(User user)
        {
            using (ISession session = NHibernateHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Save(user);
                    transaction.Commit();
                }
            }
        }

        public ICollection<User> GetAllUser()
        {
            using (ISession session = NHibernateHelper.OpenSession())
            {
                IList<User> users = session.CreateCriteria(typeof(User)).List<User>();
                return users;
            }
        }

        public User GetById(int id)
        {
            using (ISession session = NHibernateHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    User user = session.Get<User>(id);
                    transaction.Commit();
                    return user;
                }
            }
        }

        public User GetByUsername(string username)
        {
            throw new NotImplementedException();
        }

        public void Remove(User user)
        {
            using (ISession session = NHibernateHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Delete(user);
                    transaction.Commit();
                }
            }
        }

        public void Update(User user)
        {
            using (ISession session = NHibernateHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Delete(user);
                    transaction.Commit();
                }
            }
        }

        public bool VerifyUser(string username, string password)
        {
            using (ISession session = NHibernateHelper.OpenSession())
            {
                User user = session.CreateCriteria(typeof(User))
                    .Add(Restrictions.Eq("Username", username))
                    .Add(Restrictions.Eq("Password", password))
                    .UniqueResult<User>();
                if (user == null) return false;
                return true;
            }
        }
    }
}
