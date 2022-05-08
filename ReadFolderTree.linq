<Query Kind="Program" />

void Main()
{
	TraverseTree("D:\\TemplateFolder\\src\\app", "D:\\TemplateFolder\\src\\dist");
}

public static void TraverseTree(string root, string outputDir)
{
	// Data structure to hold names of subfolders to be
	// examined for files.
	Stack<string> dirs = new Stack<string>(20);

	bool exists = System.IO.Directory.Exists(outputDir);

	if (!exists)
	{
		System.IO.Directory.CreateDirectory(outputDir);
	}

	if (!System.IO.Directory.Exists(root))
	{
		throw new ArgumentException();
	}
	dirs.Push(root);

	while (dirs.Count > 0)
	{
		string currentDir = dirs.Pop();
		Console.WriteLine(currentDir);
		string[] subDirs;
		try
		{
			subDirs = System.IO.Directory.GetDirectories(currentDir);
			var folderName = new DirectoryInfo(currentDir).Name;
			var folderPath = $"{outputDir}//{folderName}";
			bool folderExists = Directory.Exists(folderPath);
			if (!folderExists)
			{
				Directory.CreateDirectory(folderPath);
			}
		}
		// An UnauthorizedAccessException exception will be thrown if we do not have
		// discovery permission on a folder or file. It may or may not be acceptable
		// to ignore the exception and continue enumerating the remaining files and
		// folders. It is also possible (but unlikely) that a DirectoryNotFound exception
		// will be raised. This will happen if currentDir has been deleted by
		// another application or thread after our call to Directory.Exists. The
		// choice of which exceptions to catch depends entirely on the specific task
		// you are intending to perform and also on how much you know with certainty
		// about the systems on which this code will run.
		catch (UnauthorizedAccessException e)
		{
			Console.WriteLine(e.Message);
			continue;
		}
		catch (System.IO.DirectoryNotFoundException e)
		{
			Console.WriteLine(e.Message);
			continue;
		}

		string[] files = null;
		try
		{
			files = System.IO.Directory.GetFiles(currentDir);
		}

		catch (UnauthorizedAccessException e)
		{

			Console.WriteLine(e.Message);
			continue;
		}

		catch (System.IO.DirectoryNotFoundException e)
		{
			Console.WriteLine(e.Message);
			continue;
		}
		// Perform the required action on each file here.
		// Modify this block to perform your required task.
		foreach (string file in files)
		{
			try
			{
				// Perform whatever action is required in your scenario.
				System.IO.FileInfo fi = new System.IO.FileInfo(file);
				Console.WriteLine("{0}: {1}, {2}, {3}", fi.Name, fi.Length, fi.CreationTime, fi.Extension);
				Console.WriteLine(Path.GetFileNameWithoutExtension(fi.Name));

				var fileName = Path.GetFileNameWithoutExtension(fi.Name);

				if (fileName.StartsWith("template"))
				{

					string path = $"{currentDir}//{fi.Name}";
					string readContent = File.ReadAllText(path);
					string newContent = readContent.Replace("$$name$$", "mdi320");
					Console.WriteLine(newContent);

					var fileNameTemplate = fi.Name.Split("_");
					if (fileNameTemplate.Count() > 1)
					{
						string newFileName = string.Format(fileNameTemplate[1], "mdi320");
						string outputPath = $"{currentDir}//{newFileName}";
						File.WriteAllText(outputPath, newContent, Encoding.UTF8);
					}

				}
			}
			catch (System.IO.FileNotFoundException e)
			{
				// If file was deleted by a separate application
				//  or thread since the call to TraverseTree()
				// then just continue.
				Console.WriteLine(e.Message);
				continue;
			}
		}

		// Push the subdirectories onto the stack for traversal.
		// This could also be done before handing the files.
		foreach (string str in subDirs)
			dirs.Push(str);
	}
}
// You can define other methods, fields, classes and namespaces here