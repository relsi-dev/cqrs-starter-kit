﻿namespace Edument.CQRS;

using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Xml.Serialization;

/// <summary>
/// This is a simple example implementation of an event store, using a SQL database
/// to provide the storage. Tested and known to work with SQL Server.
/// </summary>
public class SqlEventStore : IEventStore
{
    private string connectionString;

    public SqlEventStore(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public IEnumerable LoadEventsFor<TAggregate>(Guid id)
    {
        using (var con = new SqlConnection(connectionString))
        {
            con.Open();
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = con;
                cmd.CommandText = @"
                        SELECT [Type], [Body]
                        FROM [dbo].[Events]
                        WHERE [AggregateId] = @AggregateId
                        ORDER BY [SequenceNumber]";
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new SqlParameter("@AggregateId", id));
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        yield return DeserializeEvent(r.GetString(0), r.GetString(1));
                    }
                }
            }
        }
    }

    private object DeserializeEvent(string typeName, string data)
    {
        var ser = new XmlSerializer(Type.GetType(typeName));
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
        ms.Seek(0, SeekOrigin.Begin);
        return ser.Deserialize(ms);
    }

    public void SaveEventsFor<TAggregate>(Guid aggregateId, int eventsLoaded, ArrayList newEvents)
    {
        using (var cmd = new SqlCommand())
        {
            // Query prelude.
            var queryText = new StringBuilder(512);
            queryText.AppendLine("BEGIN TRANSACTION;");
            queryText.AppendLine(
                @"IF NOT EXISTS(SELECT * FROM [dbo].[Aggregates] WHERE [Id] = @AggregateId)
                          INSERT INTO [dbo].[Aggregates] ([Id], [Type]) VALUES (@AggregateId, @AggregateType);");
            cmd.Parameters.AddWithValue("AggregateId", aggregateId);
            cmd.Parameters.AddWithValue("AggregateType", typeof(TAggregate).AssemblyQualifiedName);

            // Add saving of the events.
            cmd.Parameters.AddWithValue("CommitDateTime", DateTime.UtcNow);
            for (int i = 0; i < newEvents.Count; i++)
            {
                var e = newEvents[i];
                queryText.AppendFormat(
                    @"INSERT INTO [dbo].[Events] ([AggregateId], [SequenceNumber], [Type], [Body], [Timestamp])
                          VALUES(@AggregateId, {0}, @Type{1}, @Body{1}, @CommitDateTime);",
                    eventsLoaded + i, i);
                cmd.Parameters.AddWithValue("Type" + i.ToString(), e.GetType().AssemblyQualifiedName);
                cmd.Parameters.AddWithValue("Body" + i.ToString(), SerializeEvent(e));
            }

            // Add commit.
            queryText.Append("COMMIT;");

            // Execute the update.
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                cmd.Connection = con;
                cmd.CommandText = queryText.ToString();
                cmd.CommandType = CommandType.Text;
                cmd.ExecuteNonQuery();
            }
        }
    }

    private string SerializeEvent(object obj)
    {
        var ser = new XmlSerializer(obj.GetType());
        var ms = new MemoryStream();
        ser.Serialize(ms, obj);
        ms.Seek(0, SeekOrigin.Begin);
        return new StreamReader(ms).ReadToEnd();
    }
}