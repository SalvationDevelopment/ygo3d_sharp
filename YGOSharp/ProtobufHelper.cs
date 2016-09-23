using System.IO;

public class ProtobufHelper
{
    public static byte[] Marshal(Google.Protobuf.IMessage obj)
    {
        var stream = new MemoryStream();
        var gStream = new Google.Protobuf.CodedOutputStream(stream);
        obj.WriteTo(gStream);
        gStream.Flush();
        var data = stream.ToArray();
        stream.Close();
        return data;
    }

    public static Google.Protobuf.IMessage Unmarshal(byte[] d, Google.Protobuf.IMessage obj)
    {
        obj.MergeFrom(new Google.Protobuf.CodedInputStream(d));
        return obj;
    }
}

