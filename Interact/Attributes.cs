using System;

namespace RabiRiichi.Interact {
    [AttributeUsage(AttributeTargets.Class)]
    public class RabiMessage : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiPrivate : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiBroadcast : Attribute { }
}