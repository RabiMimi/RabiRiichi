// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: Core/MenLike.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace RabiRiichi.Generated.Core {

  /// <summary>Holder for reflection information generated from Core/MenLike.proto</summary>
  public static partial class MenLikeReflection {

    #region Descriptor
    /// <summary>File descriptor for Core/MenLike.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static MenLikeReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChJDb3JlL01lbkxpa2UucHJvdG8aE0NvcmUvR2FtZVRpbGUucHJvdG8iKQoK",
            "TWVuTGlrZU1zZxIbCgV0aWxlcxgBIAMoCzIMLkdhbWVUaWxlTXNnQhyqAhlS",
            "YWJpUmlpY2hpLkdlbmVyYXRlZC5Db3JlYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::RabiRiichi.Generated.Core.GameTileReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::RabiRiichi.Generated.Core.MenLikeMsg), global::RabiRiichi.Generated.Core.MenLikeMsg.Parser, new[]{ "Tiles" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class MenLikeMsg : pb::IMessage<MenLikeMsg> {
    private static readonly pb::MessageParser<MenLikeMsg> _parser = new pb::MessageParser<MenLikeMsg>(() => new MenLikeMsg());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<MenLikeMsg> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::RabiRiichi.Generated.Core.MenLikeReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public MenLikeMsg() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public MenLikeMsg(MenLikeMsg other) : this() {
      tiles_ = other.tiles_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public MenLikeMsg Clone() {
      return new MenLikeMsg(this);
    }

    /// <summary>Field number for the "tiles" field.</summary>
    public const int TilesFieldNumber = 1;
    private static readonly pb::FieldCodec<global::RabiRiichi.Generated.Core.GameTileMsg> _repeated_tiles_codec
        = pb::FieldCodec.ForMessage(10, global::RabiRiichi.Generated.Core.GameTileMsg.Parser);
    private readonly pbc::RepeatedField<global::RabiRiichi.Generated.Core.GameTileMsg> tiles_ = new pbc::RepeatedField<global::RabiRiichi.Generated.Core.GameTileMsg>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::RabiRiichi.Generated.Core.GameTileMsg> Tiles {
      get { return tiles_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as MenLikeMsg);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(MenLikeMsg other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if(!tiles_.Equals(other.tiles_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      hash ^= tiles_.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      tiles_.WriteTo(output, _repeated_tiles_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      size += tiles_.CalculateSize(_repeated_tiles_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(MenLikeMsg other) {
      if (other == null) {
        return;
      }
      tiles_.Add(other.tiles_);
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            tiles_.AddEntriesFrom(input, _repeated_tiles_codec);
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
