using Meadow;
using halloween.director.shared.Controllers;
using System;
using System.Threading.Tasks;

namespace halloween.director.scarecrow.meadow.Controllers
{
    internal abstract class InitalizedBaseController : IInitalize
    {
        protected bool initialized = false;

        public virtual Task Initialize()
        {
            Resolver.Log.Info(Environment.NewLine);
            Resolver.Log.Info($"{GetType().Name} is initialized.");
            return Task.CompletedTask;
        }
    }

    internal interface IInitalize
    {
        Task Initialize();
    }
}