using Cwiczenia7.Exceptions;
using Cwiczenia7.Models;
using Cwiczenia7.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace Cwiczenia7.Services;

public interface IDbService
{
    public Task<IEnumerable<TripGetDTO>> GetTripsDetailsAsync();
    public Task<IEnumerable<ClientTripDTO>> GetTripsByClientsIdAsync(int id);
    public Task<Client> CreateClientAsync(ClientCreateDTO client);
    public Task RegisterClientForTripAsync(int id, int tripId);
    public Task RemoveRegistrationAsync(int id, int tripId);
}

public class DbService(IConfiguration config) : IDbService
{
    private readonly string? _connectionString = config.GetConnectionString("Default");

    public async Task<IEnumerable<TripGetDTO>> GetTripsDetailsAsync()
    {
        var tripsDict = new Dictionary<int, TripGetDTO>();
        await using var connection = new SqlConnection(_connectionString);
        //pobiera szczegoly wycieczek, w tym informacje o krajach zwiazanych z dana wycieczka
        const string sql = """
                        SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, c.IdCountry, c.Name as CountryName
                        FROM Trip t
                        INNER JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
                        INNER JOIN Country c ON ct.IdCountry = c.IdCountry
                        """;
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            if (!tripsDict.TryGetValue(id, out var tripDetails))
            {
                tripDetails = new TripGetDTO()
                {
                    IdTrip = id,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = new List<CountryGetDTO>()
                };
                tripsDict.Add(id, tripDetails);
            }

            if (!await reader.IsDBNullAsync(6))
            {
                tripDetails.Countries.Add(new CountryGetDTO()
                {
                    IdCountry = reader.GetInt32(6),
                    Name = reader.GetString(7),
                });
            }
        }

        return tripsDict.Values;
    }

    public async Task<IEnumerable<ClientTripDTO>> GetTripsByClientsIdAsync(int id)
    {
        var clientsTripsDict = new Dictionary<int, ClientTripDTO>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        //sprawdza czy klient o podanym id istnieje
        const string checkClientSql = "SELECT 1 FROM Client WHERE IdClient = @id";
        await using var checkCmd = new SqlCommand(checkClientSql, connection);
        checkCmd.Parameters.AddWithValue("@id", id);
        await using (var reader1 = await checkCmd.ExecuteReaderAsync())
        {
            if (!reader1.HasRows)
            {
                throw new NotFoundException($"Client with id: {id} does not exist");
            }
        }
        
        //pobiera informacje o wycieczkach na ktore jest zapisany klient o podanym id
        const string sql = """
                           SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, ct.RegisteredAt, ct.PaymentDate
                           FROM trip t
                           INNER JOIN Client_Trip ct ON t.IdTrip = ct.IdTrip
                           WHERE ct.IdClient = @id
                           """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();

        bool hasTrips = false;
        while (await reader.ReadAsync())
        {
            hasTrips = true;
            var tripId = reader.GetInt32(0);
            if (!clientsTripsDict.TryGetValue(tripId, out var tripDetails))
            {
                tripDetails = new ClientTripDTO()
                {
                    IdTrip = tripId,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    RegisteredAt = reader.GetInt32(6),
                    PaymentDate = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                };
                clientsTripsDict.Add(tripId, tripDetails);
            }
        }
        if (!hasTrips)
        {
            throw new NotFoundException($"Client with id: {id} has no trips");
        }
        return clientsTripsDict.Values;
    }

    public async Task<Client> CreateClientAsync(ClientCreateDTO client)
    {
        await using var connection = new SqlConnection(_connectionString);
        //dodaje nowego klienta do bazy danych
        const string sql = """
                           INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel) 
                           VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel); SELECT SCOPE_IDENTITY()
                           """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@FirstName", client.FirstName);
        command.Parameters.AddWithValue("@LastName", client.LastName);
        command.Parameters.AddWithValue("@Email", client.Email);
        command.Parameters.AddWithValue("@Telephone", client.Telephone);
        command.Parameters.AddWithValue("@Pesel", client.Pesel);
        await connection.OpenAsync();
        var idClient = Convert.ToInt32(await command.ExecuteScalarAsync());
        
        return new Client()
        {
            IdClient = idClient,
            FirstName = client.FirstName,
            LastName = client.LastName,
            Email = client.Email,
            Telephone = client.Telephone,
            Pesel = client.Pesel,
        };
    }

    public async Task RegisterClientForTripAsync(int id, int tripId)
    {
        await using var connection = new SqlConnection(_connectionString);

        //sprawdza czy klient o podanym id istnieje
        const string checkClient = "SELECT 1 FROM Client WHERE IdClient = @id";
        await using var checkClientCmd = new SqlCommand(checkClient, connection);
        checkClientCmd.Parameters.AddWithValue("@id", id);
        await connection.OpenAsync();
        await using (var reader1 = await checkClientCmd.ExecuteReaderAsync())
        {
            if (!reader1.HasRows)
            {
                throw new NotFoundException($"Client with id: {id} does not exist");
            }
        }

        //sprawdza czy wycieczka o podanym id istnieje
        const string checkTrip = "SELECT 1 FROM Trip WHERE IdTrip = @tripId";
        await using var checkTripCmd = new SqlCommand(checkTrip, connection);
        checkTripCmd.Parameters.AddWithValue("@tripId", tripId);
        await using (var reader2 = await checkTripCmd.ExecuteReaderAsync())
        {
            if (!reader2.HasRows)
            {
                throw new NotFoundException($"Trip with id: {tripId} does not exist");
            }
        }

        //pobiera max liczbe uczestnikow dla wycieczki o podanym id
        const string checkMaxPeople = "SELECT MaxPeople FROM Trip WHERE IdTrip = @tripId";
        await using var checkMaxPeopleCmd = new SqlCommand(checkMaxPeople, connection);
        checkMaxPeopleCmd.Parameters.AddWithValue("@tripId", tripId);
        var maxPeople = await checkMaxPeopleCmd.ExecuteScalarAsync();
        
        //pobera liczbe zapisanych uczestnikow dla wycieczki o podanym id
        const string checkCount = "SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @tripId";
        var checkCountCmd = new SqlCommand(checkCount, connection);
        checkCountCmd.Parameters.AddWithValue("@tripId", tripId);
        var countPeople = await checkCountCmd.ExecuteScalarAsync();
        if (Convert.ToInt32(countPeople) >= Convert.ToInt32(maxPeople))
        {
            throw new TripFullException($"Trip with id: {tripId} is full");
        }
        
        var todayInt = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
        //rejestruje klienta na wycieczke z dzisiesza data
        const string sql = "INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt) VALUES (@clientId, @tripId, @registeredAt)";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@clientId", id);
        cmd.Parameters.AddWithValue("@tripId", tripId);
        cmd.Parameters.AddWithValue("@registeredAt", todayInt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveRegistrationAsync(int id, int tripId)
    {
        await using var connection = new SqlConnection(_connectionString);
        
        //sprawdza czy klient jest zapisany na wycieczke
        const string checkRegistration = "SELECT 1 FROM Client_Trip WHERE IdClient = @id AND IdTrip = @tripId";
        await using var checkCmd = new SqlCommand(checkRegistration, connection);
        checkCmd.Parameters.AddWithValue("@id", id);
        checkCmd.Parameters.AddWithValue("@tripId", tripId);
        await connection.OpenAsync();
        await using (var reader = await checkCmd.ExecuteReaderAsync())
        {
            if (!reader.HasRows)
            {
                throw new NotFoundException($"Registration with client id: {id} and trip id: {tripId} does not exist");
            }
        }
        
        //usuwa rejestracje klenta z danej wycieczki
        const string sql = "DELETE FROM Client_Trip WHERE IdClient = @id AND IdTrip = @tripId";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@tripId", tripId);
        await cmd.ExecuteNonQueryAsync();
    }
}