using System;

namespace RabiRiichi.Communication {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiPrivateAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiBroadcastAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class RabiIgnoreAttribute : Attribute { }
}
