using System;
using System.Text;


namespace SyncableDict
{

	public interface IProtoComm
	{
		void SendMessage(uint id, object msg );
		void Poll( Action<uint, object> onIncomingMessage );
		ProtoTypeMap TypeMap { get; }
	}

	[ProtoBuf.ProtoContract]
	public class ProtoCreateMessage<TKey, TValue, TChgSet>
		where TChgSet : IChangeSet<TValue>
	{
		[ProtoBuf.ProtoMember(1)]
		public int RepoId;

		[ProtoBuf.ProtoMember(2)]
		public TKey Key;

		[ProtoBuf.ProtoMember(3)]
		public TChgSet ChgSet;  // must be full
	}

	[ProtoBuf.ProtoContract]
	public class ProtoUpdateMessage<TKey, TValue, TChgSet>
		where TChgSet : IChangeSet<TValue>
	{
		[ProtoBuf.ProtoMember(1)]
		public int RepoId;

		[ProtoBuf.ProtoMember(2)]
		public TKey Key;

		[ProtoBuf.ProtoMember(3)]
		public TChgSet ChgSet;  // must be full
	}

	[ProtoBuf.ProtoContract]
	public class ProtoRemoveMessage<TKey>
	{
		[ProtoBuf.ProtoMember(1)]
		public int RepoId;

		[ProtoBuf.ProtoMember(2)]
		public TKey Key;
	}

	public struct MsgIds
	{
		public uint Create;
		public uint Remove;
		public uint Update;
	}



	public class ProtoNetAdapter<TKey, TValue, TChgSet>
		where TValue : new()
		where TChgSet : IChangeSet<TValue>, new()
	{
		protected SyncRepo<TKey, TValue, TChgSet> _repo;
		protected int _repoId;
		protected IProtoComm _protoComm;
		private MsgIds _msgIds;
		public MsgIds MsgIds => _msgIds;

		public ProtoNetAdapter( SyncRepo<TKey, TValue, TChgSet> repo, int repoId, IProtoComm protoComm )
		{
			_repo = repo;
			_repoId = repoId;
			_protoComm = protoComm;

			_msgIds = RegisterMessageTypes( protoComm.TypeMap );
		}

		public static MsgIds RegisterMessageTypes(ProtoTypeMap typeMap)
		{
			return new MsgIds()
			{
				Create = typeMap.RegisterDynamicType( typeof(ProtoCreateMessage<TKey, TValue, TChgSet>) ),
				Remove = typeMap.RegisterDynamicType(typeof(ProtoCreateMessage<TKey, TValue, TChgSet>)),
				Update = typeMap.RegisterDynamicType(typeof(ProtoCreateMessage<TKey, TValue, TChgSet>)),
			};
		}

		public void Flush()
		{
			_repo.EvalChanges( OnChange );
		}

		public void Poll()
		{
			// get next incoming message from the server
			_protoComm.Poll(OnIncomingMessage);
		}


		void OnChange(SyncRepo<TKey, TValue, TChgSet>.Change chg )
		{
			if( (chg.State & SyncRepo<TKey, TValue, TChgSet>.EState.Added) != 0 )
			{
				var x = new ProtoCreateMessage<TKey, TValue, TChgSet>()
				{
					RepoId = _repoId,
					Key = chg.Key,
					ChgSet = chg.ChgSet
				};
				_protoComm.SendMessage( _msgIds.Create, x);
			}
			else
			if( (chg.State & SyncRepo<TKey, TValue, TChgSet>.EState.Removed) != 0 )
			{
				var x = new ProtoRemoveMessage<TKey>()
				{
					RepoId = _repoId,
					Key = chg.Key
				};
				_protoComm.SendMessage( _msgIds.Remove, x);
			}
			else
			{
				var x = new ProtoUpdateMessage<TKey, TValue, TChgSet>()
				{
					RepoId = _repoId,
					Key = chg.Key,
					ChgSet = chg.ChgSet
				};
				_protoComm.SendMessage( _msgIds.Update, x );
			}
		}

		void OnIncomingMessage( uint msgId, object boxedMsg )
		{
			if( msgId == _msgIds.Create )
			{
				var msg = boxedMsg as ProtoCreateMessage<TKey, TValue, TChgSet>;
				var val = new TValue();
				msg.ChgSet.ToValue(val);
				_repo.Add(msg.Key, val, false);
			}
			else
			if( msgId == _msgIds.Remove )
			{
				var msg = boxedMsg as ProtoRemoveMessage<TKey>;
				_repo.Remove(msg.Key);
			}
			else
			if( msgId == _msgIds.Update )
			{
				var msg = boxedMsg as ProtoUpdateMessage<TKey, TValue, TChgSet>;
				var val = new TValue();
				msg.ChgSet.ToValue(val);
				_repo.Update(msg.Key, val);
			}
		}
	}


}
