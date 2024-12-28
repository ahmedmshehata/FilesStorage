using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Data.SQLite;

namespace BusinessLogic
{
    public class BusinessLogicClass
    {
        public void Execute()
        {

            string sqliteConnectionString = "Data Source=images.db;Version=3;";

            // Initialize ImageStorage class
            var imageStorage = new ImageStorage(sqliteConnectionString);

            // Create necessary tables in the database (run once to set up the DB)
            imageStorage.CreateTables();

            // Process images and store details
            imageStorage.CompressImagesToZip();

            // Example of retrieving image details by ID
            int imageId = 120;// 1; // Example image ID
            var imageDetails = imageStorage.GetImageDetailsById(imageId);

            // Export the image to a custom path
            string exportPath = @"C:\ImagesTest\ExportedImages";
            imageStorage.ExportImageFromZip(imageId, exportPath);

            imageStorage.CloseDatabase();
            /*
            string sqliteConnectionString = "Data Source=images.db;Version=3;";
        
        // Initialize ImageStorage class
        var imageStorage = new ImageStorage(sqliteConnectionString);
        
        // Create necessary tables in the database (run once to set up the DB)
        imageStorage.CreateTables();

        // Process images and store details
        imageStorage.CompressImagesToZip();
        
        // Example of retrieving image details by ID
        int imageId = 1; // Example image ID
        var imageDetails = imageStorage.GetImageDetailsById(imageId);

        // Export the image to a custom path
        string exportPath = @"C:\ImagesTest\ExportedImages";
        imageStorage.ExportImageFromZip(imageDetails.ZipFilePath, imageDetails.IndexInArchive, exportPath);

        imageStorage.CloseDatabase();


            string sqliteConnectionString = "Data Source=images.db;Version=3;";
        
        // Initialize ImageStorage class
        var imageStorage = new ImageStorage(sqliteConnectionString);
        
        // Create necessary tables in the database (run once to set up the DB)
        imageStorage.CreateTables();

        // Process images and store details
        imageStorage.CompressImagesToZip();
        imageStorage.CloseDatabase();
*/
            Console.WriteLine("Executing business logic...");
            // Add your business logic here
        }
    }

    public class ImageStorage
    {
        private string _imagesDirectory = @"C:\ImagesTest\Images";  // Directory where your images are stored
        private string _zipDirectory = @"C:\ImagesTest\ZippedImages";  // Directory where ZIP files will be stored
        private SQLiteConnection _sqliteConnection;

        public ImageStorage(string sqliteConnectionString)
        {
            // Initialize SQLite database connection
            _sqliteConnection = new SQLiteConnection(sqliteConnectionString);
            _sqliteConnection.Open();
        }

        // Method to create the necessary tables (run once to set up the DB)
        public void CreateTables()
        {
            // Create Images table to store ZIP file paths
            string createImagesTableQuery = @"
            CREATE TABLE IF NOT EXISTS Images (
                Id INTEGER PRIMARY KEY,
                ZipFilePath TEXT
            )";
            ExecuteQuery(createImagesTableQuery);

            // Create ImageDetails table to store image names and their index in the archive,
            // with a foreign key referencing the Images(Id)
            string createDetailsTableQuery = @"
            CREATE TABLE IF NOT EXISTS ImageDetails (
                Id INTEGER PRIMARY KEY,
                ImageId INTEGER,  -- Foreign key referencing Images(Id)
                ImageName TEXT,
                IndexInArchive INTEGER,
                FOREIGN KEY (ImageId) REFERENCES Images(Id)
            )";
            ExecuteQuery(createDetailsTableQuery);
        }

        // Helper method to execute queries
        private void ExecuteQuery(string query)
        {
            using (var command = new SQLiteCommand(query, _sqliteConnection))
            {
                command.ExecuteNonQuery();
            }
        }

        // Method to compress images into ZIP files
        public void CompressImagesToZip()
        {
            try
            {
                if(!Directory.Exists(_imagesDirectory))
                {
                    Directory.CreateDirectory(_imagesDirectory);
                }
                if(!Directory.Exists(_zipDirectory))
                {
                    Directory.CreateDirectory(_zipDirectory);
                }
            }
            catch (System.Exception)
            {
                return;
                //throw;
            }
          
            var imageFiles = Directory.GetFiles(_imagesDirectory, "*.png"); // Adjust extension as needed /*.jpg, *.png, *.jpeg*/
            if(imageFiles.Length == 0)
            {
                return;
            }
            int batchSize = 100;  // Number of images per ZIP file
            int currentBatch = 1;
            var currentJob = Guid.NewGuid().ToString();
            //string currentTime = DateTime.Now.ToString("yyyyMMddHHmm");
            currentJob = DateTime.Now.ToString("yyyyMMddHHmmss");

            // Iterate through image files and group them into batches
            List<string> batchFiles = new List<string>();
            foreach (var imageFile in imageFiles)
            {
                batchFiles.Add(imageFile);

                if (batchFiles.Count >= batchSize)
                {
                    string zipFileName = $"{_zipDirectory}\\batch_{currentJob}_{currentBatch}.zip";
                    CreateZip(batchFiles, zipFileName);

                    // Store path in the SQLite database and get ImageId
                    int imageId = StoreZipInDatabase(zipFileName);

                    // Store image details in the ImageDetails table
                    StoreImageDetails(imageId, batchFiles);

                    batchFiles.Clear(); // Reset for the next batch
                    currentBatch++;
                }
            }

            // Handle the last batch if it has less than 'batchSize' images
            if (batchFiles.Count > 0)
            {
                string zipFileName = $"{_zipDirectory}\\batch_{currentJob}_{currentBatch}.zip";
                CreateZip(batchFiles, zipFileName);
                int imageId = StoreZipInDatabase(zipFileName);
                StoreImageDetails(imageId, batchFiles);
            }
        }

        // Method to create a ZIP file from a list of image files
        private void CreateZip(List<string> imageFiles, string zipFileName)
        {
            // Ensure the zip file doesn't already exist
            if (File.Exists(zipFileName))
            {
                File.Delete(zipFileName); // Delete if exists to avoid errors
            }

            // Create a ZIP file and add images
            using (ZipArchive zip = ZipFile.Open(zipFileName, ZipArchiveMode.Create))
            {
                foreach (var imageFile in imageFiles)
                {
                    zip.CreateEntryFromFile(imageFile, Path.GetFileName(imageFile));
                }
            }

            Console.WriteLine($"Created ZIP file: {zipFileName}");
        }

        // Method to store the ZIP file path in the Images table and return the ImageId
        private int StoreZipInDatabase(string zipFilePath)
        {
            string insertQuery = "INSERT INTO Images (ZipFilePath) VALUES (@ZipFilePath)";

            using (var command = new SQLiteCommand(insertQuery, _sqliteConnection))
            {
                command.Parameters.AddWithValue("@ZipFilePath", zipFilePath);
                command.ExecuteNonQuery();

                // Return the last inserted Id (ImageId)
                return (int)_sqliteConnection.LastInsertRowId;
            }
        }

        // Method to store the image details (name, index, and ImageId) in the ImageDetails table
        private void StoreImageDetails(int imageId, List<string> imageFiles)
        {
            string insertQuery = "INSERT INTO ImageDetails (ImageId, ImageName, IndexInArchive) VALUES (@ImageId, @ImageName, @IndexInArchive)";

            // Insert each image name and its index in the ZIP
            for (int index = 0; index < imageFiles.Count; index++)
            {
                string imageName = Path.GetFileName(imageFiles[index]);

                using (var command = new SQLiteCommand(insertQuery, _sqliteConnection))
                {
                    command.Parameters.AddWithValue("@ImageId", imageId);
                    command.Parameters.AddWithValue("@ImageName", imageName);
                    command.Parameters.AddWithValue("@IndexInArchive", index + 1); // Index starts at 1 in the archive
                    command.ExecuteNonQuery();
                }

                Console.WriteLine($"Stored image details: {imageName}, Index: {index + 1}, ImageId: {imageId}");
            }
        }

        // Method to get image details by ID
        //public (string ImageName, int IndexInArchive, string ZipFilePath) GetImageDetailsById(int id)
        public (string ImageName, int IndexInArchive, int ImageId) GetImageDetailsById(int id)
        {
            string query = "SELECT ImageId, ImageName, IndexInArchive FROM ImageDetails WHERE Id = @Id";

            using (var command = new SQLiteCommand(query, _sqliteConnection))
            {
                command.Parameters.AddWithValue("@Id", id);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (

                            ImageName: reader.GetString(1),
                            IndexInArchive: reader.GetInt32(2),
                            ImageId: reader.GetInt32(0)
                        );
                    }
                    else
                    {
                        throw new Exception("Image not found.");
                    }
                }
            }
        }

        // Method to extract the image from the ZIP archive and export it to a custom path
        public void ExportImageFromZip(int imageId, string exportPath)
        {
            // Get image details by ID
            var imageDetails = GetImageDetailsById(imageId);

            // Get the corresponding ZIP file path from the Images table using ImageId
            string zipFilePath = GetZipFilePathById(imageDetails.ImageId);

            // Extract image from the ZIP file
            using (ZipArchive zip = ZipFile.OpenRead(zipFilePath))
            {
                // Find the entry based on the index in archive
                ZipArchiveEntry entry = zip.Entries[imageDetails.IndexInArchive - 1]; // Index is 1-based
                string extractedFilePath = Path.Combine(exportPath, entry.FullName);

                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(extractedFilePath));

                // Extract the file to the custom path
                entry.ExtractToFile(extractedFilePath, overwrite: true);

                Console.WriteLine($"Exported {entry.FullName} to {extractedFilePath}");
            }
        }

        // Method to get the ZipFilePath from Images table by ImageId
        private string GetZipFilePathById(int imageId)
        {
            string query = "SELECT ZipFilePath FROM Images WHERE Id = @Id";

            using (var command = new SQLiteCommand(query, _sqliteConnection))
            {
                command.Parameters.AddWithValue("@Id", imageId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetString(0);
                    }
                    else
                    {
                        throw new Exception("Zip file path not found.");
                    }
                }
            }
        }

        // Method to close the SQLite connection
        public void CloseDatabase()
        {
            _sqliteConnection.Close();
        }
    }




    public class ImageStorage2
    {
        private string _imagesDirectory = @"C:\ImagesTest\Images";  // Directory where your images are stored
        private string _zipDirectory = @"C:\ImagesTest\ZippedImages";  // Directory where ZIP files will be stored
        private SQLiteConnection _sqliteConnection;

        public ImageStorage2(string sqliteConnectionString)
        {
            // Initialize SQLite database connection
            _sqliteConnection = new SQLiteConnection(sqliteConnectionString);
            _sqliteConnection.Open();
        }

        // Method to create the necessary tables (run once to set up the DB)
        public void CreateTables()
        {
            // Create Images table to store ZIP file paths
            string createImagesTableQuery = @"
            CREATE TABLE IF NOT EXISTS Images (
                Id INTEGER PRIMARY KEY,
                ZipFilePath TEXT
            )";
            ExecuteQuery(createImagesTableQuery);

            // Create ImageDetails table to store image names and their index in the archive
            string createDetailsTableQuery = @"
            CREATE TABLE IF NOT EXISTS ImageDetails (
                Id INTEGER PRIMARY KEY,
                ImageName TEXT,
                IndexInArchive INTEGER,
                ZipFilePath TEXT,
                FOREIGN KEY (ZipFilePath) REFERENCES Images(ZipFilePath)
            )";
            ExecuteQuery(createDetailsTableQuery);
        }

        // Helper method to execute queries
        private void ExecuteQuery(string query)
        {
            using (var command = new SQLiteCommand(query, _sqliteConnection))
            {
                command.ExecuteNonQuery();
            }
        }

        // Method to compress images into ZIP files
        public void CompressImagesToZip()
        {
            var imageFiles = Directory.GetFiles(_imagesDirectory, "*.png"); // Adjust extension as needed  /*.jpg, *.png, *.jpeg*/
            int batchSize = 100;  // Number of images per ZIP file
            int currentBatch = 1;

            // Iterate through image files and group them into batches
            List<string> batchFiles = new List<string>();
            foreach (var imageFile in imageFiles)
            {
                batchFiles.Add(imageFile);

                if (batchFiles.Count >= batchSize)
                {
                    string zipFileName = $"{_zipDirectory}\\batch_{currentBatch}.zip";
                    CreateZip(batchFiles, zipFileName);

                    // Store path in the SQLite database
                    StoreZipInDatabase(zipFileName);

                    // Store image details in the ImageDetails table
                    StoreImageDetails(zipFileName, batchFiles);

                    batchFiles.Clear(); // Reset for the next batch
                    currentBatch++;
                }
            }

            // Handle the last batch if it has less than 'batchSize' images
            if (batchFiles.Count > 0)
            {
                string zipFileName = $"{_zipDirectory}\\batch_{currentBatch}.zip";
                CreateZip(batchFiles, zipFileName);
                StoreZipInDatabase(zipFileName);
                StoreImageDetails(zipFileName, batchFiles);
            }
        }

        // Method to create a ZIP file from a list of image files
        private void CreateZip(List<string> imageFiles, string zipFileName)
        {
            // Ensure the zip file doesn't already exist
            if (File.Exists(zipFileName))
            {
                File.Delete(zipFileName); // Delete if exists to avoid errors
            }

            // Create a ZIP file and add images
            using (ZipArchive zip = ZipFile.Open(zipFileName, ZipArchiveMode.Create))
            {
                foreach (var imageFile in imageFiles)
                {
                    zip.CreateEntryFromFile(imageFile, Path.GetFileName(imageFile));
                }
            }

            Console.WriteLine($"Created ZIP file: {zipFileName}");
        }

        // Method to store the ZIP file path in the Images table
        private void StoreZipInDatabase(string zipFilePath)
        {
            string insertQuery = "INSERT INTO Images (ZipFilePath) VALUES (@ZipFilePath)";

            using (var command = new SQLiteCommand(insertQuery, _sqliteConnection))
            {
                command.Parameters.AddWithValue("@ZipFilePath", zipFilePath);
                command.ExecuteNonQuery();
            }

            Console.WriteLine($"Stored ZIP file path in database: {zipFilePath}");
        }

        // Method to store the image details (name and index) in the ImageDetails table
        private void StoreImageDetails(string zipFilePath, List<string> imageFiles)
        {
            string insertQuery = "INSERT INTO ImageDetails (ImageName, IndexInArchive, ZipFilePath) VALUES (@ImageName, @IndexInArchive, @ZipFilePath)";

            // Insert each image name and its index in the ZIP
            for (int index = 0; index < imageFiles.Count; index++)
            {
                string imageName = Path.GetFileName(imageFiles[index]);

                using (var command = new SQLiteCommand(insertQuery, _sqliteConnection))
                {
                    command.Parameters.AddWithValue("@ImageName", imageName);
                    command.Parameters.AddWithValue("@IndexInArchive", index + 1); // Index starts at 1 in the archive
                    command.Parameters.AddWithValue("@ZipFilePath", zipFilePath);
                    command.ExecuteNonQuery();
                }

                Console.WriteLine($"Stored image details: {imageName}, Index: {index + 1}, Zip: {zipFilePath}");
            }
        }

        // Method to close the SQLite connection
        public void CloseDatabase()
        {
            _sqliteConnection.Close();
        }
    }

}
