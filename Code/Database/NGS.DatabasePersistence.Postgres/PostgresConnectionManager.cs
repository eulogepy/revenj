﻿using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using NGS.Logging;
using Npgsql;

namespace NGS.DatabasePersistence.Postgres
{
	public interface IConnectionManager
	{
		NpgsqlConnection Create(bool open);
		void Release(NpgsqlConnection connection, bool valid);
	}

	public class PostgresConnectionManager : IConnectionManager
	{
		private readonly BlockingCollection<NpgsqlConnection> Connections = new BlockingCollection<NpgsqlConnection>(new ConcurrentBag<NpgsqlConnection>());

		public enum PoolMode
		{
			None,
			Wait,
			IfAvailable
		}

		private readonly PoolMode Mode = PoolMode.IfAvailable;
		private readonly ConnectionInfo Info;
		private readonly int Size;

		private readonly ILogger Logger;

		public PostgresConnectionManager(ConnectionInfo info, ILogFactory logFactory)
		{
			this.Info = info;
			if (!int.TryParse(ConfigurationManager.AppSettings["Database.PoolSize"], out Size))
				Size = 20;
			if (!Enum.TryParse<PoolMode>(ConfigurationManager.AppSettings["Database.PoolMode"], out Mode))
				Mode = PoolMode.IfAvailable;
			if (Mode != PoolMode.None)
			{
				if (Size < 1) Size = 1;
				for (int i = 0; i < Size; i++)
					Connections.Add(info.GetConnection());
			}
			this.Logger = logFactory.Create("Npgsql connection manager");
		}


		public NpgsqlConnection Create(bool open)
		{
			NpgsqlConnection conn;
			switch (Mode)
			{
				case PoolMode.None:
					conn = Info.GetConnection();
					break;
				case PoolMode.Wait:
					conn = Connections.Take();
					break;
				default:
					if (!Connections.TryTake(out conn))
						conn = Info.GetConnection();
					break;
			}
			if (open)
			{
				try
				{
					if (conn.State == ConnectionState.Closed)
						conn.Open();
				}
				catch (Exception ex)
				{
					Logger.Error(ex.ToString());
					try { conn.Close(); }
					catch { }
					NpgsqlConnection.ClearAllPools();
					conn = Info.GetConnection();
					conn.Open();
				}
			}
			return conn;
		}

		public void Release(NpgsqlConnection connection, bool valid)
		{
			switch (Mode)
			{
				case PoolMode.None:
					try { connection.Close(); }
					catch (Exception ex)
					{
						if (valid)
							Logger.Error("Error closing valid connection: " + ex.ToString());
					}
					break;
				default:
					if (valid && connection.State == ConnectionState.Open)
					{
						if (Connections.Count < Size)
							Connections.Add(connection);
					}
					else
					{
						try { connection.Close(); }
						catch (Exception ex)
						{
							if (valid)
								Logger.Error("Error closing valid connection: " + ex.ToString());
						}
						if (Connections.Count < Size)
							Connections.Add(Info.GetConnection());
					}
					break;
			}
		}
	}
}
