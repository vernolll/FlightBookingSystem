using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Configuration;

namespace FlightBookingSystemServer
{
    public class Flight
    {
        public int Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public DateTime Date { get; set; }
    }

    public class FlightServer
    {
        private readonly string _connectionString;
        private readonly TcpListener _listener;
        private bool _isRunning;

        public FlightServer(int port)
        {
            _connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            _isRunning = true;
            _listener.Start();
            Console.WriteLine("Сервер запущен и ожидает подключения.");

            while (_isRunning)
            {
                var client = _listener.AcceptTcpClient();
                var thread = new Thread(HandleClient);
                thread.Start(client);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
            Console.WriteLine("Сервер остановлен.");
        }

        private void HandleClient(object obj)
        {
            var client = (TcpClient)obj;
            Console.WriteLine($"DEBUG: Клиент подключен: {client.Client.RemoteEndPoint}");
            var stream = client.GetStream();
            var buffer = new byte[4096];
            int bytesRead;

            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"DEBUG: Получен запрос: {request}");

                    var response = ProcessRequest(request);
                    Console.WriteLine($"DEBUG: Отправка ответа: {response}");

                    var responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                    stream.Write(responseBytes, 0, responseBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine("DEBUG: Подключение клиента закрыто");
            }
        }

        private string ProcessRequest(string request)
        {
            var parts = request.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "ERROR: Invalid request";

            var command = parts[0].ToUpper();
            try
            {
                switch (command)
                {
                    case "BOOK_SEAT":
                        return HandleBookSeat(parts);
                    case "GET_USER":
                        return HandleGetUserInfo(parts);
                    case "GET_SEATS":
                        return HandleGetSeats(parts);
                    case "LOGIN":
                        return HandleLogin(parts);
                    case "REGISTER":
                        return HandleRegister(parts);
                    case "GET_FLIGHTS":
                        return HandleGetFlights();
                    case "CHANGE_PASSWORD":  
                        return HandleChangePassword(parts);
                    case "UPDATE_USER":
                        return HandleChangeInfo(parts);
                    default:
                        return "ERROR: Unknown command";
                }
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        private string HandleChangeInfo(string[] parts)
        {
            if (parts.Length < 5) return "ERROR: Missing parameters"; // Проверяем, что передано достаточно параметров
            if (!int.TryParse(parts[1], out var userId)) return "ERROR: Invalid user ID"; // Проверяем корректность ID

            var firstName = parts[2];
            var lastName = parts[3];
            if (!int.TryParse(parts[4], out var age)) return "ERROR: Invalid age"; // Проверяем корректность возраста

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            // Проверяем, существует ли запись для указанного пользователя в таблице UsersInfo
            var checkUserInfoCommand = new SqlCommand(
                "SELECT COUNT(*) FROM UsersInfo WHERE user_Id = @userId", connection);
            checkUserInfoCommand.Parameters.AddWithValue("@userId", userId);

            var recordExists = Convert.ToInt32(checkUserInfoCommand.ExecuteScalar()) > 0;

            if (recordExists)
            {
                // Если запись существует, обновляем её
                var updateUserInfoCommand = new SqlCommand(
                    "UPDATE UsersInfo SET [1Name] = @firstName, [2Name] = @lastName, Age = @age WHERE user_Id = @userId", connection);
                updateUserInfoCommand.Parameters.AddWithValue("@firstName", firstName);
                updateUserInfoCommand.Parameters.AddWithValue("@lastName", lastName);
                updateUserInfoCommand.Parameters.AddWithValue("@age", age);
                updateUserInfoCommand.Parameters.AddWithValue("@userId", userId);

                var rowsAffected = updateUserInfoCommand.ExecuteNonQuery();
                return rowsAffected > 0 ? "SUCCESS: User info updated" : "ERROR: Could not update user info";
            }
            else
            {
                // Если записи не существует, добавляем новую
                var insertUserInfoCommand = new SqlCommand(
                    "INSERT INTO UsersInfo (user_Id, [1Name], [2Name], Age) VALUES (@userId, @firstName, @lastName, @age)", connection);
                insertUserInfoCommand.Parameters.AddWithValue("@userId", userId);
                insertUserInfoCommand.Parameters.AddWithValue("@firstName", firstName);
                insertUserInfoCommand.Parameters.AddWithValue("@lastName", lastName);
                insertUserInfoCommand.Parameters.AddWithValue("@age", age);

                try
                {
                    insertUserInfoCommand.ExecuteNonQuery();
                    return "SUCCESS: User info added";
                }
                catch (Exception ex)
                {
                    return $"ERROR: {ex.Message}";
                }
            }
        }


        private string HandleLogin(string[] parts)
        {
            if (parts.Length < 3) return "ERROR: Missing parameters";
            var username = parts[1];
            var password = parts[2];

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var command = new SqlCommand(
                "SELECT Id FROM Users WHERE Username = @username AND Password = @password", connection);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@password", password);

            var result = command.ExecuteScalar();
            return result != null ? $"SUCCESS={result}" : "ERROR: Invalid credentials";
        }

        private string HandleRegister(string[] parts)
        {
            if (parts.Length < 3) return "ERROR: Missing parameters";
            var username = parts[1];
            var password = parts[2];

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var command = new SqlCommand(
                "INSERT INTO Users (Username, Password) VALUES (@username, @password)", connection);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@password", password);

            try
            {
                command.ExecuteNonQuery();
                return "SUCCESS: User registered";
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                return "ERROR: Username already exists";
            }
        }

        // Метод для получения информации о пользователе
        private string HandleGetUserInfo(string[] parts)
        {
            if (parts.Length < 2) return "ERROR: Missing parameters";
            if (!int.TryParse(parts[1], out var userId)) return "ERROR: Invalid user ID";

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var command = new SqlCommand(
                "SELECT [1Name], [2Name], [Age] FROM UsersInfo WHERE user_Id = @userId", connection);
            command.Parameters.AddWithValue("@userId", userId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var name = reader["1Name"] != DBNull.Value ? reader["1Name"].ToString() : "?";
                var surname = reader["2Name"] != DBNull.Value ? reader["2Name"].ToString() : "?";
                var age = reader["Age"] != DBNull.Value ? reader["Age"].ToString() : "-1";

                return $"USER={name},{surname},{age}";
            }

            return "ERROR: User not found";
        }

        private string HandleGetFlights()
        {
            var flights = new List<Flight>();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();

                    string query = "SELECT id, [from], [to], [date] FROM planes";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                flights.Add(new Flight
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    From = reader["from"].ToString(),
                                    To = reader["to"].ToString(),
                                    Date = Convert.ToDateTime(reader["date"])
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при загрузке рейсов: {ex.Message}");
                }
            }

            var result = new StringBuilder();
            if (flights.Count == 0)
            {
                result.Append("ERROR: No flights available");
            }
            else
            {
                foreach (var flight in flights)
                {
                    result.AppendLine($"Flight ID: {flight.Id}, From: {flight.From}, To: {flight.To}, Date: {flight.Date:dd.MM.yyyy}");
                }
            }

            return result.ToString();
        }

        private string HandleGetSeats(string[] parts)
        {
            if (parts.Length < 2) return "ERROR: Missing parameters";
            if (!int.TryParse(parts[1], out var flightId)) return "ERROR: Invalid flight ID";

            var availableSeats = GetAvailableSeatsWithPrices(flightId); // Метод теперь возвращает список кортежей

            return availableSeats.Count > 0
                ? $"Seats: {string.Join(", ", availableSeats.Select(seat => $"[{seat.SeatNumber}-Price:{seat.Price:F2}]"))}"
                : "ERROR: No available seats";
        }

        private List<(string SeatNumber, decimal Price)> GetAvailableSeatsWithPrices(int flightId)
        {
            var seatsWithPrices = new List<(string SeatNumber, decimal Price)>();

            // Добавим отладочную информацию
            Console.WriteLine($"Запрос на получение мест для рейса с ID {flightId}");

            string query = "SELECT number, Price FROM seats WHERE plane_id = @flightId AND isFree = 1";

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(query, connection))
                {
                    // Добавление параметра для фильтрации по flightId
                    command.Parameters.AddWithValue("@flightId", flightId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string seatNumber = reader.GetString(0);  // Индекс 0 для поля 'number'
                            decimal price = reader.GetDecimal(1); // Индекс 1 для поля 'Price'

                            // Логирование каждого найденного места
                            Console.WriteLine($"Найдено место: {seatNumber}, Цена: {price}");

                            seatsWithPrices.Add((seatNumber, price));
                        }
                    }
                }
            }

            // Логирование результата выполнения запроса
            if (seatsWithPrices.Count == 0)
            {
                Console.WriteLine($"Для рейса с ID {flightId} не найдено доступных мест.");
            }
            else
            {
                Console.WriteLine($"Найдено {seatsWithPrices.Count} доступных мест.");
            }

            return seatsWithPrices;
        }
        private string HandleBookSeat(string[] parts)
        {
            if (parts.Length < 3) return "ERROR: Missing parameters";
            if (!int.TryParse(parts[1], out var flightId)) return "ERROR: Invalid flight ID";
            var seatNumber = parts[2].Trim(); // Убираем лишние пробелы с номера сиденья

            // Отладка: выводим информацию о запросе
            Console.WriteLine($"Получен запрос на бронирование места: Flight ID = {flightId}, Seat = {seatNumber}");

            // Проверяем, существует ли рейс
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var flightExistsCommand = new SqlCommand(
                "SELECT COUNT(*) FROM planes WHERE id = @plane_id", connection);
            flightExistsCommand.Parameters.AddWithValue("@plane_id", flightId);
            var flightExists = Convert.ToInt32(flightExistsCommand.ExecuteScalar()) > 0;

            if (!flightExists)
            {
                Console.WriteLine($"Рейс {flightId} не существует.");
                return "ERROR: Flight does not exist";
            }

            // Проверяем, существует ли сиденье (сравниваем как строки)
            var seatExistsCommand = new SqlCommand(
                "SELECT COUNT(*) FROM seats WHERE plane_id = @plane_id AND number = @seat_number", connection);
            seatExistsCommand.Parameters.AddWithValue("@plane_id", flightId);
            seatExistsCommand.Parameters.AddWithValue("@seat_number", seatNumber);

            // Отладочный вывод запроса
            Console.WriteLine($"SQL запрос для проверки существования места: SELECT COUNT(*) FROM seats WHERE plane_id = {flightId} AND number = '{seatNumber}'");

            var seatExists = Convert.ToInt32(seatExistsCommand.ExecuteScalar()) > 0;

            if (!seatExists)
            {
                Console.WriteLine($"Место {seatNumber} для рейса {flightId} не найдено.");
                return "ERROR: Seat does not exist";
            }

            // Проверяем, свободно ли сиденье
            var seatFreeCommand = new SqlCommand(
                "SELECT isFree FROM seats WHERE plane_id = @plane_id AND number = @seat_number", connection);
            seatFreeCommand.Parameters.AddWithValue("@plane_id", flightId);
            seatFreeCommand.Parameters.AddWithValue("@seat_number", seatNumber);
            var isFree = Convert.ToBoolean(seatFreeCommand.ExecuteScalar());

            if (!isFree)
            {
                Console.WriteLine($"Место {seatNumber} уже забронировано.");
                return "ERROR: Seat is already booked";
            }

            // Бронируем сиденье
            var bookSeatCommand = new SqlCommand(
                "UPDATE seats SET isFree = 0 WHERE plane_id = @plane_id AND number = @seat_number", connection);
            bookSeatCommand.Parameters.AddWithValue("@plane_id", flightId);
            bookSeatCommand.Parameters.AddWithValue("@seat_number", seatNumber);

            var rowsAffected = bookSeatCommand.ExecuteNonQuery();
            return rowsAffected > 0 ? $"SUCCESS: Seat {seatNumber} booked for flight {flightId}" : "ERROR: Could not book seat";
        }


        private string HandleChangePassword(string[] parts)
        {
            if (parts.Length < 3) return "ERROR: Missing parameters";  // Проверяем, что параметры переданы
            var username = parts[1];  // Никнейм пользователя
            var newPassword = parts[2];  // Новый пароль

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            // Проверяем, существует ли пользователь с таким никнеймом
            var checkUserCommand = new SqlCommand(
                "SELECT COUNT(*) FROM Users WHERE Username = @username", connection);
            checkUserCommand.Parameters.AddWithValue("@username", username);

            var userExists = Convert.ToInt32(checkUserCommand.ExecuteScalar()) > 0;

            if (!userExists)
            {
                return "ERROR: User not found";  // Если пользователь с таким никнеймом не найден
            }

            // Обновляем пароль на новый
            var updatePasswordCommand = new SqlCommand(
                "UPDATE Users SET Password = @newPassword WHERE Username = @username", connection);
            updatePasswordCommand.Parameters.AddWithValue("@username", username);
            updatePasswordCommand.Parameters.AddWithValue("@newPassword", newPassword);

            var rowsAffected = updatePasswordCommand.ExecuteNonQuery();
            return rowsAffected > 0 ? "SUCCESS: Password updated" : "ERROR: Could not update password";
        }

        public static void Main(string[] args)
        {
            var server = new FlightServer(8080);
            server.Start();
        }
    }
}
