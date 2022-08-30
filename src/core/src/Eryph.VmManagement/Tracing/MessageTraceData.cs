using System.Collections.Generic;


namespace Eryph.VmManagement;

public class MessageTraceData : TraceData
{
    public override string Type => "Message";

    public static MessageTraceData FromObject<T>(T message, string messageId)
    {

        var data = new Dictionary<string, object>
        {
            {"messageId", messageId },
            {"messageType", typeof(T).FullName},
            {"message", message}
        };

        return new MessageTraceData
        {
            Data = data
        };

    }
}