using System;
using System.Data.SqlClient;

namespace FlightBookingSystem.Models
{
    public class DatabaseManager
    {
        private string connectionString;

        public DatabaseManager()
        {
            // Получение строки подключения из App.config
            connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        // Метод для проверки логина и пароля
        public bool Login(string username, string password, out int userId)
        {
            userId = -1;  // Значение по умолчанию, если пользователь не найден
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = "SELECT id FROM users WHERE username = @username AND password = @password";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    command.Parameters.AddWithValue("@password", password);

                    var result = command.ExecuteScalar();
                    if (result != null)
                    {
                        userId = (int)result; // Получаем userId
                        return true;  // Логин успешен
                    }
                    return false; // Логин неудачен
                }
            }
        }

        // Метод для регистрации пользователя
        public bool Register(string username, string password)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Проверяем, существует ли пользователь
                string checkQuery = "SELECT COUNT(*) FROM users WHERE username = @username";
                using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@username", username);
                    int count = (int)checkCommand.ExecuteScalar();

                    if (count > 0)
                        return false; // Пользователь уже существует
                }

                // Если пользователь не существует, регистрируем его
                string insertQuery = "INSERT INTO users (username, password) VALUES (@username, @password)";
                using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                {
                    insertCommand.Parameters.AddWithValue("@username", username);
                    insertCommand.Parameters.AddWithValue("@password", password);

                    insertCommand.ExecuteNonQuery();
                    return true; // Регистрация успешна
                }
            }
        }

        // Метод для получения информации о пользователе (имя, фамилия, возраст)
        public (string Name, string Surname, int Age) GetUserInfo(int userId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Используем экранированные имена столбцов с квадратными скобками
                string query = "SELECT [1Name], [2Name], [Age] FROM usersInfo WHERE user_id = @userId";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string name = reader["1Name"] != DBNull.Value ? reader["1Name"].ToString() : "?";
                            string surname = reader["2Name"] != DBNull.Value ? reader["2Name"].ToString() : "?";
                            int age = reader["Age"] != DBNull.Value ? Convert.ToInt32(reader["Age"]) : -1;

                            return (name, surname, age);
                        }
                        else
                        {
                            return ("?", "?", -1);  // Если пользователь не найден, возвращаем значения по умолчанию
                        }
                    }
                }
            }
        }


        // Метод для обновления информации о пользователе (имя, фамилия, возраст)
        public bool UpdateUserInfo(int userId, string name, string surname, int age)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Сначала проверим, существует ли пользователь в таблице usersInfo
                string checkQuery = "SELECT COUNT(*) FROM usersInfo WHERE user_id = @userId";
                using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@userId", userId);
                    int count = (int)checkCommand.ExecuteScalar();

                    if (count == 0)
                    {
                        // Если пользователя нет в таблице, создаём запись
                        string insertQuery = "INSERT INTO usersInfo (user_id, [1name], [2name], [age]) VALUES (@userId, @name, @surname, @age)";
                        using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@userId", userId);
                            insertCommand.Parameters.AddWithValue("@name", name);
                            insertCommand.Parameters.AddWithValue("@surname", surname);
                            insertCommand.Parameters.AddWithValue("@age", age);

                            insertCommand.ExecuteNonQuery();
                            return true;  // Успешно создали нового пользователя в usersInfo
                        }
                    }
                    else
                    {
                        // Если пользователь существует, обновляем его данные
                        string updateQuery = "UPDATE usersInfo SET [1name] = @name, [2name] = @surname, [age] = @age WHERE user_id = @userId";
                        using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@userId", userId);
                            updateCommand.Parameters.AddWithValue("@name", name);
                            updateCommand.Parameters.AddWithValue("@surname", surname);
                            updateCommand.Parameters.AddWithValue("@age", age);

                            int rowsAffected = updateCommand.ExecuteNonQuery();
                            return rowsAffected > 0;  // Если обновление прошло успешно
                        }
                    }
                }
            }

        }

        public List<Flight> GetFlights()
        {
            var flights = new List<Flight>();
            using (SqlConnection connection = new SqlConnection(connectionString))
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
            return flights;
        }

        // Метод для получения доступных мест (свободных мест) для рейса
        public List<string> GetAvailableSeats(int flightId)
        {
            var seats = new List<string>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    Console.WriteLine($"Opened database connection for flight {flightId}");

                    // Запрос к базе данных для получения свободных мест
                    string query = "SELECT number FROM seats WHERE plane_id = @plane_id AND isFree = 0";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@plane_id", flightId);
                        Console.WriteLine($"Executing query: {query} with plane_id = {flightId}");

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var seat = reader["number"].ToString();
                                seats.Add(seat);
                                Console.WriteLine($"Found available seat: {seat}");
                            }
                        }
                    }

                    if (seats.Count == 0)
                    {
                        Console.WriteLine("No available seats found.");
                    }
                    else
                    {
                        Console.WriteLine($"Available seats: {string.Join(", ", seats)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while fetching available seats: {ex.Message}");
                }
            }
            return seats;
        }

        // Обработчик команды GET_SEATS
        private string HandleGetSeats(string[] parts)
        {
            if (parts.Length < 2)
            {
                Console.WriteLine("ERROR: Missing parameters");
                return "ERROR: Missing parameters";
            }

            if (!int.TryParse(parts[1], out var flightId))
            {
                Console.WriteLine("ERROR: Invalid flight ID");
                return "ERROR: Invalid flight ID";
            }

            Console.WriteLine($"Fetching available seats for flight {flightId}");

            var availableSeats = GetAvailableSeats(flightId);

            if (availableSeats.Count == 0)
            {
                Console.WriteLine("No available seats for the flight.");
                return "ERROR: No available seats";
            }

            Console.WriteLine($"Seats available: {string.Join(", ", availableSeats)}");
            return $"Seats available: {string.Join(", ", availableSeats)}";
        }


        // Обработчик для бронирования места
        private string HandleBookSeat(string[] parts)
        {
            if (parts.Length < 3)
            {
                Console.WriteLine("ERROR: Missing parameters");
                return "ERROR: Missing parameters";
            }

            if (!int.TryParse(parts[1], out var flightId))
            {
                Console.WriteLine("ERROR: Invalid flight ID");
                return "ERROR: Invalid flight ID";
            }

            if (!int.TryParse(parts[2], out var seatNumber))
            {
                Console.WriteLine("ERROR: Invalid seat number");
                return "ERROR: Invalid seat number";
            }

            Console.WriteLine($"Attempting to book seat {seatNumber} for flight {flightId}");

            var availableSeats = GetAvailableSeats(flightId);

            if (!availableSeats.Contains(seatNumber.ToString()))
            {
                Console.WriteLine($"ERROR: Seat {seatNumber} is not available for booking.");
                return "ERROR: Seat not available";
            }

            // Бронирование места
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    Console.WriteLine($"Opened database connection to book seat {seatNumber} for flight {flightId}");

                    var command = new SqlCommand(
                        "UPDATE seats SET isFree = 0 WHERE plane_id = @plane_id AND number = @seat_number AND isFree = 1", connection);
                    command.Parameters.AddWithValue("@plane_id", flightId);
                    command.Parameters.AddWithValue("@seat_number", seatNumber);

                    var rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"SUCCESS: Seat {seatNumber} booked for flight {flightId}");
                        return $"SUCCESS: Seat {seatNumber} booked for flight {flightId}";
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: Could not book seat {seatNumber} for flight {flightId}. Seat might be already booked.");
                        return "ERROR: Could not book seat";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while booking seat {seatNumber} for flight {flightId}: {ex.Message}");
                    return $"ERROR: {ex.Message}";
                }
            }
        }

        public bool BookSeat(int flightId, string seatNumber)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "UPDATE seats SET isFree = 1 WHERE plane_id = @plane_id AND number = @Number AND isFree = 0";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@plane_id", flightId);
                    command.Parameters.AddWithValue("@Number", seatNumber);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        // Метод для проверки текущего пароля
        public bool CheckCurrentPassword(int userId, string currentPassword)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Выполняем запрос для получения текущего пароля пользователя по его userId
                string query = "SELECT password FROM users WHERE id = @userId";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);

                    // Получаем текущий пароль пользователя
                    var result = command.ExecuteScalar();

                    // Если результат не пустой и пароли совпадают, возвращаем true
                    if (result != null && result.ToString() == currentPassword)
                    {
                        return true;  // Текущий пароль правильный
                    }

                    return false;  // Текущий пароль неверный
                }
            }
        }


        // Метод для смены пароля
        private bool UpdatePassword(int userId, string newPassword)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = "UPDATE users SET password = @password WHERE id = @userId";  // Запрос на обновление пароля
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@password", newPassword);  // Новый пароль
                    command.Parameters.AddWithValue("@userId", userId);  // ID пользователя

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;  // Если обновление прошло успешно, возвращаем true
                }
            }
        }

        public bool ChangePasswordByNickname(string nickname, string newPassword)
        {
            var user = GetUserByNickname(nickname);  // Получаем пользователя по нику
            if (user != null)
            {
                // Если пользователь найден, обновляем его пароль
                return UpdatePassword(user.Id, newPassword);  // Используем метод UpdatePassword для изменения пароля
            }
            return false;  // Если пользователь не найден
        }

        // Пример метода для получения пользователя по нику (предполагаем, что база данных поддерживает такие запросы)
        private User GetUserByNickname(string nickname)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = "SELECT id, username FROM users WHERE username = @nickname";  // запрос на поиск пользователя по нику
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@nickname", nickname);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())  // Если нашли пользователя
                        {
                            return new User
                            {
                                Id = Convert.ToInt32(reader["id"]),  // Получаем ID пользователя
                                Nickname = reader["username"].ToString()  // Получаем никнейм пользователя
                            };
                        }
                        else
                        {
                            return null;  // Если пользователя с таким ником нет
                        }
                    }
                }
            }
        }
    }
}

// Модель рейса
public class Flight
{
    public int Id { get; set; }
    public string From { get; set; }
    public string To { get; set; }
    public DateTime Date { get; set; }
}


public class User
{
    public int Id { get; set; }
    public string Nickname { get; set; }
}