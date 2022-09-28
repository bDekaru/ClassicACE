using ACE.Entity.Enum.Properties;

namespace ACE.Entity
{
    public struct PartialPersistanceEntry
    {
        public PropertyType PropertyType;
        public ushort Property;

        public PartialPersistanceEntry(PropertyBool propertyEntry)
        {
            PropertyType = PropertyType.PropertyBool;
            Property = (ushort)propertyEntry;
        }

        public PartialPersistanceEntry(PropertyDataId propertyEntry)
        {
            PropertyType = PropertyType.PropertyDataId;
            Property = (ushort)propertyEntry;
        }

        public PartialPersistanceEntry(PropertyFloat propertyEntry)
        {
            PropertyType = PropertyType.PropertyFloat;
            Property = (ushort)propertyEntry;
        }

        public PartialPersistanceEntry(PropertyInstanceId propertyEntry)
        {
            PropertyType = PropertyType.PropertyInstanceId;
            Property = (ushort)propertyEntry;
        }

        public PartialPersistanceEntry(PropertyInt propertyEntry)
        {
            PropertyType = PropertyType.PropertyInt;
            Property = (ushort)propertyEntry;
        }

        public PartialPersistanceEntry(PropertyInt64 propertyEntry)
        {
            PropertyType = PropertyType.PropertyInt64;
            Property = (ushort)propertyEntry;
        }

        public PartialPersistanceEntry(PropertyString propertyEntry)
        {
            PropertyType = PropertyType.PropertyString;
            Property = (ushort)propertyEntry;
        }
    }
}
