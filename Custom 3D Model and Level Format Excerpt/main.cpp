
//#include "fbxStuff.h"
#include "Utility.h"
#include "FbxToAbmConverter.h"
#include "FbxToLrmConverter.h"
#include "FbxToLrlConverter.h"

#include <thread>

#ifndef LIB_CONFIG
int main()
{
	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
	std::srand((unsigned)time(0));

	FbxManager* lSdkManager = NULL;
	FbxScene* scene = NULL;
	bool lResult;

	// Prepare the FBX SDK.
	InitializeSdkObjects(lSdkManager, scene);
	
	FbxAxisSystem::DirectX.ConvertScene(scene);
	
	FbxString lFilePath("");

	std::unordered_map<std::string, int> files; // map of files and their last modified timestamp, only used when keepRunning == true.

	const bool keepRunning = true;
	const int SLEEP_TIME = 7000;

	const bool justDisplayContent = false; //make this true to see a version of the fbxdemo
	
	const bool convertToLRM = true; // If false, the program will use .abm

	const bool levelConvert = true;
	const bool alwaysConvertLevelMeshes = false;

	const bool convertToLeftHanded = false;

	while (true)
	{
		bool nothingConverted = true;
		
		if (levelConvert)
		{
			std::string levelPath = "../Levels/To be converted";

			for (const auto& entry : std::filesystem::directory_iterator(levelPath))
			{
				std::string filename = entry.path().generic_string();

				if (filename.find(".fbx") == std::string::npos) // Check if it's an fbx file, if not continue
				{
					//std::cout << std::endl << filename << " is not a convertable file" << std::endl;
					continue;
				}
				if (filename.find(".fbxa") != std::string::npos) // Check if it's an fbxa0 temp file, if so skip 
				{
					continue;
				}
				if (filename.find(".bak") != std::string::npos) // Check if it's an bak temp file, if so skip 
				{
					continue;
				}

				if (keepRunning)
				{
					// Check if the file is in the map of files
					if (files.find(filename) != files.end())
					{
						// It is already in the map
						// check if it has been modifyed, if so run, otherwise skip it
						struct stat result; int mod_time = 0;
						if (stat(filename.c_str(), &result) == 0)
							mod_time = result.st_mtime;
						else
							assert(false);

						if (mod_time == files[filename])
						{
							// It has not been modifyed and has already been converted. Skip it.
							continue;
						}
						else
							files[filename] = mod_time;
					}
					else
					{
						// if it wasn't in the map we add it and run the converter
						struct stat result;
						int mod_time = 0;

						if (stat(filename.c_str(), &result) == 0)
							mod_time = result.st_mtime;
						else
							assert(false);

						files[filename] = mod_time;
					}
				}

				std::cout << "-------------------------------------------------------------------------" << std::endl;
				std::cout << "-Level Convert-" << std::endl;
				std::cout << entry.path() << " " << std::endl;

				lFilePath = (filename).c_str();

				// Load the scene
				lResult = LoadScene(lSdkManager, scene, lFilePath.Buffer());

				if (lResult == false)
				{
					FBXSDK_printf("\n\nAn error occurred while loading the scene...");
				}
				else
				{
					if (convertToLeftHanded && scene->GetGlobalSettings().GetAxisSystem().GetCoorSystem() == FbxAxisSystem::eRightHanded)
					{
						FbxAxisSystem::DirectX.ConvertScene(scene); // This doesn't do what we want -_-
					}

					if (justDisplayContent)
						DisplayContent(scene);
					else
					{
						// The load was successful, time to find things to convert
						convertSceneFbxToLevel(scene, filename);
						if(alwaysConvertLevelMeshes)
							convertSceneFbxToLRM(scene, filename);
					}
						
				}

				nothingConverted = false;
			}
		}


		// ------------------------------------------------------------
		// ---- Loop through all the files in the following folder ----
		// ------------------------------------------------------------
		std::string path = "../Models/To be converted";
		for (const auto& entry : std::filesystem::directory_iterator(path))
		{
			std::string filename = entry.path().generic_string();

			if (filename.find(".fbx") == std::string::npos) // Check if it's an fbx file, if not continue
			{
				//std::cout << std::endl << filename << " is not a convertable file" << std::endl;
				continue;
			}
			if (filename.find(".fbxa") != std::string::npos) // Check if it's an fbxa0 temp file, if so skip 
			{
				continue;
			}

			if (keepRunning)
			{
				// Check if the file is in the map of files
				if (files.find(filename) != files.end())
				{
					// It is already in the map

					// check if it has been modifyed, if so run, otherwise skip it

					struct stat result; int mod_time = 0;
					if (stat(filename.c_str(), &result) == 0)
						mod_time = result.st_mtime;
					else
						assert(false);

					if (mod_time == files[filename])
					{
						// It has not been modifyed and has already been converted. Skip it.
						continue;
					}
					else
						files[filename] = mod_time;
				}
				else
				{
					// if it wasn't in the map we add it and run the converter
					struct stat result;
					int mod_time = 0;

					if (stat(filename.c_str(), &result) == 0)
						mod_time = result.st_mtime;
					else
						assert(false);

					files[filename] = mod_time;
				}
			}

			std::cout << "-------------------------------------------------------------------------" << std::endl;
			std::cout << "-Model Convert-" << std::endl;
			std::cout << entry.path() << " " << std::endl;

			lFilePath = (filename).c_str();

			// Load the scene
			lResult = LoadScene(lSdkManager, scene, lFilePath.Buffer());
			std::cout << "Done loading the scene." << std::endl;

			if (lResult == false)
			{
				FBXSDK_printf("\n\nAn error occurred while loading the scene...");
			}
			else
			{
				if (convertToLeftHanded && scene->GetGlobalSettings().GetAxisSystem().GetCoorSystem() == FbxAxisSystem::eRightHanded)
				{
					FbxAxisSystem::DirectX.ConvertScene(scene); // This doesn't do what we want -_-
				}

				if (justDisplayContent)
					DisplayContent(scene);
				else
					// The load was successful, time to find things to convert
					if (convertToLRM)
						convertSceneFbxToLRM(scene, filename);
					else
						convertSceneFbxToAbm(scene, filename);
			}

			nothingConverted = false;
		}

		if (!keepRunning)
			break;
		else
		{
			if (!nothingConverted) 
			{
				std::cout << "-------------------------------------------------------------------------" << std::endl;
				std::cout << "All done, will now sleep and wait for file modifications." << std::endl;
			}
			else
				std::cout << "Sleeping..." << std::endl;

			std::this_thread::sleep_for(std::chrono::milliseconds(SLEEP_TIME));
		}	
	}

	std::cout << "all done";
	getchar();
	return 0;
}
#endif
