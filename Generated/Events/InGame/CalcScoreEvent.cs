// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: Events/InGame/CalcScoreEvent.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace RabiRiichi.Generated.Events.InGame {

  /// <summary>Holder for reflection information generated from Events/InGame/CalcScoreEvent.proto</summary>
  public static partial class CalcScoreEventReflection {

    #region Descriptor
    /// <summary>File descriptor for Events/InGame/CalcScoreEvent.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static CalcScoreEventReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiJFdmVudHMvSW5HYW1lL0NhbGNTY29yZUV2ZW50LnByb3RvGh5FdmVudHMv",
            "SW5HYW1lL0FnYXJpRXZlbnQucHJvdG8iYgoQU2NvcmVUcmFuc2Zlck1zZxIM",
            "CgRmcm9tGAEgASgFEgoKAnRvGAIgASgFEg4KBnBvaW50cxgDIAEoAxIkCgZy",
            "ZWFzb24YBCABKA4yFC5TY29yZVRyYW5zZmVyUmVhc29uImQKEUNhbGNTY29y",
            "ZUV2ZW50TXNnEiYKC2FnYXJpX2luZm9zGAEgASgLMhEuQWdhcmlJbmZvTGlz",
            "dE1zZxInCgxzY29yZV9jaGFuZ2UYAiADKAsyES5TY29yZVRyYW5zZmVyTXNn",
            "KoYCChNTY29yZVRyYW5zZmVyUmVhc29uEh0KGVNDT1JFX1RSQU5TRkVSX1JF",
            "QVNPTl9ST04QABIfChtTQ09SRV9UUkFOU0ZFUl9SRUFTT05fVFNVTU8QARIj",
            "Ch9TQ09SRV9UUkFOU0ZFUl9SRUFTT05fUllVVUtZT0tVEAISKAokU0NPUkVf",
            "VFJBTlNGRVJfUkVBU09OX05BR0FTSElfTUFOR0FOEAMSIAocU0NPUkVfVFJB",
            "TlNGRVJfUkVBU09OX1JJSUNISRAEEh8KG1NDT1JFX1RSQU5TRkVSX1JFQVNP",
            "Tl9IT05CQRAFEh0KGVNDT1JFX1RSQU5TRkVSX1JFQVNPTl9QQU8QBkIlqgIi",
            "UmFiaVJpaWNoaS5HZW5lcmF0ZWQuRXZlbnRzLkluR2FtZWIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::RabiRiichi.Generated.Events.InGame.AgariEventReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::RabiRiichi.Generated.Events.InGame.ScoreTransferReason), }, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::RabiRiichi.Generated.Events.InGame.ScoreTransferMsg), global::RabiRiichi.Generated.Events.InGame.ScoreTransferMsg.Parser, new[]{ "From", "To", "Points", "Reason" }, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::RabiRiichi.Generated.Events.InGame.CalcScoreEventMsg), global::RabiRiichi.Generated.Events.InGame.CalcScoreEventMsg.Parser, new[]{ "AgariInfos", "ScoreChange" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Enums
  public enum ScoreTransferReason {
    /// <summary>
    /// 荣和
    /// </summary>
    [pbr::OriginalName("SCORE_TRANSFER_REASON_RON")] Ron = 0,
    /// <summary>
    /// 自摸
    /// </summary>
    [pbr::OriginalName("SCORE_TRANSFER_REASON_TSUMO")] Tsumo = 1,
    /// <summary>
    /// 流局
    /// </summary>
    [pbr::OriginalName("SCORE_TRANSFER_REASON_RYUUKYOKU")] Ryuukyoku = 2,
    /// <summary>
    /// 流局满贯
    /// </summary>
    [pbr::OriginalName("SCORE_TRANSFER_REASON_NAGASHI_MANGAN")] NagashiMangan = 3,
    /// <summary>
    /// 立直
    /// </summary>
    [pbr::OriginalName("SCORE_TRANSFER_REASON_RIICHI")] Riichi = 4,
    /// <summary>
    /// 本场棒
    /// </summary>
    [pbr::OriginalName("SCORE_TRANSFER_REASON_HONBA")] Honba = 5,
    /// <summary>
    /// 包牌
    /// </summary>
    [pbr::OriginalName("SCORE_TRANSFER_REASON_PAO")] Pao = 6,
  }

  #endregion

  #region Messages
  public sealed partial class ScoreTransferMsg : pb::IMessage<ScoreTransferMsg> {
    private static readonly pb::MessageParser<ScoreTransferMsg> _parser = new pb::MessageParser<ScoreTransferMsg>(() => new ScoreTransferMsg());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<ScoreTransferMsg> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::RabiRiichi.Generated.Events.InGame.CalcScoreEventReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ScoreTransferMsg() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ScoreTransferMsg(ScoreTransferMsg other) : this() {
      from_ = other.from_;
      to_ = other.to_;
      points_ = other.points_;
      reason_ = other.reason_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ScoreTransferMsg Clone() {
      return new ScoreTransferMsg(this);
    }

    /// <summary>Field number for the "from" field.</summary>
    public const int FromFieldNumber = 1;
    private int from_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int From {
      get { return from_; }
      set {
        from_ = value;
      }
    }

    /// <summary>Field number for the "to" field.</summary>
    public const int ToFieldNumber = 2;
    private int to_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int To {
      get { return to_; }
      set {
        to_ = value;
      }
    }

    /// <summary>Field number for the "points" field.</summary>
    public const int PointsFieldNumber = 3;
    private long points_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public long Points {
      get { return points_; }
      set {
        points_ = value;
      }
    }

    /// <summary>Field number for the "reason" field.</summary>
    public const int ReasonFieldNumber = 4;
    private global::RabiRiichi.Generated.Events.InGame.ScoreTransferReason reason_ = 0;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::RabiRiichi.Generated.Events.InGame.ScoreTransferReason Reason {
      get { return reason_; }
      set {
        reason_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as ScoreTransferMsg);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(ScoreTransferMsg other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (From != other.From) return false;
      if (To != other.To) return false;
      if (Points != other.Points) return false;
      if (Reason != other.Reason) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (From != 0) hash ^= From.GetHashCode();
      if (To != 0) hash ^= To.GetHashCode();
      if (Points != 0L) hash ^= Points.GetHashCode();
      if (Reason != 0) hash ^= Reason.GetHashCode();
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
      if (From != 0) {
        output.WriteRawTag(8);
        output.WriteInt32(From);
      }
      if (To != 0) {
        output.WriteRawTag(16);
        output.WriteInt32(To);
      }
      if (Points != 0L) {
        output.WriteRawTag(24);
        output.WriteInt64(Points);
      }
      if (Reason != 0) {
        output.WriteRawTag(32);
        output.WriteEnum((int) Reason);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (From != 0) {
        size += 1 + pb::CodedOutputStream.ComputeInt32Size(From);
      }
      if (To != 0) {
        size += 1 + pb::CodedOutputStream.ComputeInt32Size(To);
      }
      if (Points != 0L) {
        size += 1 + pb::CodedOutputStream.ComputeInt64Size(Points);
      }
      if (Reason != 0) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) Reason);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(ScoreTransferMsg other) {
      if (other == null) {
        return;
      }
      if (other.From != 0) {
        From = other.From;
      }
      if (other.To != 0) {
        To = other.To;
      }
      if (other.Points != 0L) {
        Points = other.Points;
      }
      if (other.Reason != 0) {
        Reason = other.Reason;
      }
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
          case 8: {
            From = input.ReadInt32();
            break;
          }
          case 16: {
            To = input.ReadInt32();
            break;
          }
          case 24: {
            Points = input.ReadInt64();
            break;
          }
          case 32: {
            reason_ = (global::RabiRiichi.Generated.Events.InGame.ScoreTransferReason) input.ReadEnum();
            break;
          }
        }
      }
    }

  }

  public sealed partial class CalcScoreEventMsg : pb::IMessage<CalcScoreEventMsg> {
    private static readonly pb::MessageParser<CalcScoreEventMsg> _parser = new pb::MessageParser<CalcScoreEventMsg>(() => new CalcScoreEventMsg());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<CalcScoreEventMsg> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::RabiRiichi.Generated.Events.InGame.CalcScoreEventReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CalcScoreEventMsg() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CalcScoreEventMsg(CalcScoreEventMsg other) : this() {
      agariInfos_ = other.agariInfos_ != null ? other.agariInfos_.Clone() : null;
      scoreChange_ = other.scoreChange_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CalcScoreEventMsg Clone() {
      return new CalcScoreEventMsg(this);
    }

    /// <summary>Field number for the "agari_infos" field.</summary>
    public const int AgariInfosFieldNumber = 1;
    private global::RabiRiichi.Generated.Events.InGame.AgariInfoListMsg agariInfos_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::RabiRiichi.Generated.Events.InGame.AgariInfoListMsg AgariInfos {
      get { return agariInfos_; }
      set {
        agariInfos_ = value;
      }
    }

    /// <summary>Field number for the "score_change" field.</summary>
    public const int ScoreChangeFieldNumber = 2;
    private static readonly pb::FieldCodec<global::RabiRiichi.Generated.Events.InGame.ScoreTransferMsg> _repeated_scoreChange_codec
        = pb::FieldCodec.ForMessage(18, global::RabiRiichi.Generated.Events.InGame.ScoreTransferMsg.Parser);
    private readonly pbc::RepeatedField<global::RabiRiichi.Generated.Events.InGame.ScoreTransferMsg> scoreChange_ = new pbc::RepeatedField<global::RabiRiichi.Generated.Events.InGame.ScoreTransferMsg>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::RabiRiichi.Generated.Events.InGame.ScoreTransferMsg> ScoreChange {
      get { return scoreChange_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as CalcScoreEventMsg);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(CalcScoreEventMsg other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(AgariInfos, other.AgariInfos)) return false;
      if(!scoreChange_.Equals(other.scoreChange_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (agariInfos_ != null) hash ^= AgariInfos.GetHashCode();
      hash ^= scoreChange_.GetHashCode();
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
      if (agariInfos_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(AgariInfos);
      }
      scoreChange_.WriteTo(output, _repeated_scoreChange_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (agariInfos_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(AgariInfos);
      }
      size += scoreChange_.CalculateSize(_repeated_scoreChange_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(CalcScoreEventMsg other) {
      if (other == null) {
        return;
      }
      if (other.agariInfos_ != null) {
        if (agariInfos_ == null) {
          agariInfos_ = new global::RabiRiichi.Generated.Events.InGame.AgariInfoListMsg();
        }
        AgariInfos.MergeFrom(other.AgariInfos);
      }
      scoreChange_.Add(other.scoreChange_);
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
            if (agariInfos_ == null) {
              agariInfos_ = new global::RabiRiichi.Generated.Events.InGame.AgariInfoListMsg();
            }
            input.ReadMessage(agariInfos_);
            break;
          }
          case 18: {
            scoreChange_.AddEntriesFrom(input, _repeated_scoreChange_codec);
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
