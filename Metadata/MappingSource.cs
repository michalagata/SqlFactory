using System;
using System.Collections.Generic;
using System.Threading;
namespace AnubisWorks.SQLFactory.Metadata
{
    internal abstract class MappingSource
    {
        MetaModel primaryModel;
        ReaderWriterLock rwlock;
        Dictionary<Type, MetaModel> secondaryModels;
        public MetaModel GetModel(Type dataContextType)
        {
            if (dataContextType == null) throw Error.ArgumentNull(nameof(dataContextType));
            MetaModel model = null;
            if (this.primaryModel == null)
            {
                model = CreateModel(dataContextType);
                Interlocked.CompareExchange<MetaModel>(ref this.primaryModel, model, null);
            }
            if (this.primaryModel.ContextType == dataContextType)
            {
                return this.primaryModel;
            }
            if (this.secondaryModels == null)
            {
                Interlocked.CompareExchange<Dictionary<Type, MetaModel>>(ref this.secondaryModels, new Dictionary<Type, MetaModel>(), null);
            }
            if (this.rwlock == null)
            {
                Interlocked.CompareExchange<ReaderWriterLock>(ref this.rwlock, new ReaderWriterLock(), null);
            }
            MetaModel foundModel;
            this.rwlock.AcquireReaderLock(Timeout.Infinite);
            try
            {
                if (this.secondaryModels.TryGetValue(dataContextType, out foundModel))
                {
                    return foundModel;
                }
            }
            finally
            {
                this.rwlock.ReleaseReaderLock();
            }
            this.rwlock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                if (this.secondaryModels.TryGetValue(dataContextType, out foundModel))
                {
                    return foundModel;
                }
                if (model == null)
                {
                    model = CreateModel(dataContextType);
                }
                this.secondaryModels.Add(dataContextType, model);
            }
            finally
            {
                this.rwlock.ReleaseWriterLock();
            }
            return model;
        }
        protected abstract MetaModel CreateModel(Type dataContextType);
    }
    internal sealed class AttributeMappingSource : MappingSource
    {
        public AttributeMappingSource() { }
        protected override MetaModel CreateModel(Type dataContextType)
        {
            if (dataContextType == null) throw Error.ArgumentNull(nameof(dataContextType));
            return new AttributedMetaModel(this, dataContextType);
        }
    }
}