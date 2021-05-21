namespace PhotonCloud.Authentication.Configuration
{
    using System.Configuration;

    public class MasterServerElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new MasterServerElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((MasterServerElement)element).InternalIpAddress;
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.BasicMap; }
        }

        protected override string ElementName
        {
            get { return "Server"; }
        }

        public MasterServerElement this[int index]
        {
            get
            {
                return (MasterServerElement)this.BaseGet(index);
            }
        }
    }
}
