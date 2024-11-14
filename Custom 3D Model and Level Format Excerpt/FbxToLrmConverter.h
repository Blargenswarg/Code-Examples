#pragma once
#include <fstream>


void convertSceneFbxToLRM(FbxScene* pScene, std::string filename);
void convertContentFbxToLRM(FbxNode* pNode, std::string filename, std::vector<std::string>& convertedMeshes, std::vector<std::string>& meshesWithoutUniqueName);
void convertMeshFbxToLRM(FbxNode* pNode, std::string filename);
void convertLightFbxToLRM(FbxNode* pNode, std::string filename);
void convertCameraFbxToLRC(FbxNode* pNode, std::string filename);

bool copyFile(const char* source, const char* destination);

void getAllJoints(Joint** finalJointsPtr, std::unordered_map<std::string, int>* boneIdxs, FbxNode* linkNode);
void getAllKeyFrames(std::vector<float>* keyFrameTimes, int& endFrame, std::unordered_map<std::string, int>* boneIdxs, FbxNode* linkNode, FbxAnimLayer* animLayer);
void getEndTime(double& endTime, FbxNode* linkNode, FbxAnimLayer* animLayer);
//void getAnimationFrames(Animation* currentAnimation, FbxNode* linkNode, double animationStopTime, double frameRate);
void getAnimationFrames(AnimationFrame* currentFrame, std::unordered_map<std::string, int>* boneIdxs, FbxNode* linkNode, fbxsdk::FbxTime* frameTime, FbxAnimEvaluator* eval);

// Loops through all the nodes in the loaded scene and calls convertContentFbxToLRM() with them
void convertSceneFbxToLRM(FbxScene* pScene, std::string filename)
{
	FbxNode* lNode = pScene->GetRootNode();

	std::vector<std::string> convertedMeshes;
	std::vector<std::string> meshesWithoutUniqueName;

	if (lNode)
	{
		for (int i = 0; i < lNode->GetChildCount(); i++)
		{
			convertContentFbxToLRM(lNode->GetChild(i), filename, convertedMeshes, meshesWithoutUniqueName);
		}
	}

	if (convertedMeshes.size() > 5)
	{
		std::cout << "All meshes without unique names: " << std::endl;
		for (int i = 0; i < meshesWithoutUniqueName.size(); i++)
		{
			std::cout << meshesWithoutUniqueName.at(i) << std::endl;
		}
	}

}
// Checks the type of node and converts it if it's a mesh
// Also recursively calls itself if the node has a child
void convertContentFbxToLRM(FbxNode* pNode, std::string filename, std::vector<std::string>& convertedMeshes, std::vector<std::string>& meshesWithoutUniqueName)
{
	FbxNodeAttribute::EType lAttributeType;
	if (pNode->GetNodeAttribute() == NULL)
	{
		FBXSDK_printf("NULL Node Attribute\n\n");
	}
	else
	{
		lAttributeType = (pNode->GetNodeAttribute()->GetAttributeType());
		//std::cout << "node name: " << pNode->GetName() << " node type:" << lAttributeType << std::endl;
		switch (lAttributeType)
		{
		default:
			break;

		case FbxNodeAttribute::eMesh:     // If the type is mesh

			if (pNode->GetName() == std::string("dummy_mesh"))
				break;

			std::cout << "--- Converting mesh: " << pNode->GetName() << " ---" << std::endl;

			bool alreadyConverted = false;

			FbxProperty uniqueNameParam = pNode->FindProperty(std::string("uniqueName").c_str(), false);
			if (uniqueNameParam.IsValid())
			{
				std::string uniqueName = uniqueNameParam.Get<FbxString>().Buffer(); // If the mesh has an attribute describing a unique name, we use that

				if (uniqueName != "")
				{
					std::cout << "Unique name: " << uniqueName << std::endl;

					if (std::find(convertedMeshes.begin(), convertedMeshes.end(), uniqueName) != convertedMeshes.end())
					{
						alreadyConverted = true;
						std::cout << "The mesh " << uniqueName << " has already been converted. Skipping." << std::endl;
						std::cout << std::endl;
					}
					else
						convertedMeshes.push_back(uniqueName);
				}
				else
				{
					std::cout << "Unique name: [none]" << std::endl;
					meshesWithoutUniqueName.push_back(pNode->GetName());
				}
			}
			else
			{
				std::cout << "Unique name: [no parameter]" << std::endl;
				meshesWithoutUniqueName.push_back(pNode->GetName());
			}

			if (!alreadyConverted)
				convertMeshFbxToLRM(pNode, filename); // Convert it and write a file

			break;

			/*
			case FbxNodeAttribute::eLight:     // If the type is light

				std::cout << "--- Converting light: " << pNode->GetName() << " ---" << std::endl;

				convertLightFbxToLRM(pNode, filename); // Convert it and write a file
				break;

			case FbxNodeAttribute::eCamera:     // If the type is camera

				std::cout << "--- Converting camera: " << pNode->GetName() << " ---" << std::endl;

				convertCameraFbxToLRC(pNode, filename); // Convert it and write a file
				break;
			*/
		}
	}

	for (int i = 0; i < pNode->GetChildCount(); i++)
	{
		convertContentFbxToLRM(pNode->GetChild(i), filename, convertedMeshes, meshesWithoutUniqueName);
	}
}

void convertMeshFbxToLRM(FbxNode* pNode, std::string filename)
{
	std::cout << "Reading info...";

	// Change this values and recompile if needed
	bool flipVertOrder = false; // default is CCW (I think), so flipped is CW.
	const bool flipTexCoordU = false;
	const bool flipTexCoordV = true;
	bool flipAllX = true;

	FbxMesh* mesh = (FbxMesh*)pNode->GetNodeAttribute();

	// --------------------------------------------
	// ---- Count the amount of various things ----
	// --------------------------------------------
	std::uint32_t posNr = mesh->GetControlPointsCount();
	std::uint32_t uvNr;
	std::uint32_t nNr;
	std::uint32_t polygonNr = mesh->GetPolygonCount();
	std::uint32_t vertNr = mesh->GetPolygonVertexCount();
	std::uint32_t skinNr = mesh->GetDeformerCount(FbxDeformer::eSkin);
	std::uint32_t boneNr; // also known as clusterNr or clustercount
	std::uint32_t matNr = 0;
	bool oneMaterial = true;

	if (mesh->GetElementUVCount() > 1)
		std::cout << std::endl << "!Mesh has several uv channels!";

	FbxGeometryElementUV* leUV = mesh->GetElementUV(0);
	FbxGeometryElementNormal* leNormal = mesh->GetElementNormal(0);
	FbxGeometryElementTangent* leTangent = mesh->GetElementTangent(0);
	FbxGeometryElementBinormal* leBitangent = mesh->GetElementBinormal(0);

	switch (leUV->GetMappingMode())
	{
	case FbxGeometryElement::eByControlPoint:
		uvNr = posNr;
		break;
	case FbxGeometryElement::eByPolygonVertex:
		uvNr = leUV->GetDirectArray().GetCount();
		break;
	default:
		assert(false);
	}

	if (mesh->GetElementNormalCount() > 1)
		assert(false);	// If it asserts here you have done something very weird so that the mesh has multiple sets of normals
	else
		nNr = leNormal->GetDirectArray().GetCount();

	bool isRigged = false;
	if (skinNr > 0) // if the mesh is skinned, i think i can assume it should only ever have one or zero
	{
		isRigged = true;
		if (skinNr > 1)
			assert(false); // mesh has multiple skins
	}

	if (isRigged)
		boneNr = ((FbxSkin*)mesh->GetDeformer(0, FbxDeformer::eSkin))->GetClusterCount(); // clustercount is bonecount
	else
		boneNr = 0;

	if (isRigged)
		flipAllX = false; // TEMP UNTIL FLIPALLX IS FIXED

	if (flipAllX)
		flipVertOrder = !flipVertOrder; //This is jank but seems to work for now

	int doneNr = 0;
	bool hasNoUvs = false;
	std::uint32_t rootJointIdx = 0;

	if (uvNr == 0)
	{
		hasNoUvs = true;
		uvNr = 3;
	}

	if (mesh->GetElementMaterialCount() != 0)
	{
		if (mesh->GetElementMaterialCount() > 1)
			assert(false);
		else
		{
			FbxGeometryElementMaterial* lMaterialElement = mesh->GetElementMaterial(0);

			for (int i = 0; i < lMaterialElement->GetIndexArray().GetCount(); i++)
			{
				if (lMaterialElement->GetIndexArray().GetAt(i) >= matNr)
					matNr = lMaterialElement->GetIndexArray().GetAt(i) + 1;
			}

			if (lMaterialElement->GetMappingMode() == FbxGeometryElement::eByPolygon)
			{
				oneMaterial = false;
			}
		}
	}
	else
		matNr = 1;

	assert(pNode->GetMaterialCount() == matNr);

	std::cout << std::endl << "Nr of Polygonvertices: " << vertNr;
	std::cout << std::endl << "Nr of polygons: " << polygonNr;
	std::cout << std::endl << "Nr of positions: " << posNr;
	std::cout << std::endl << "Nr of uvs: " << uvNr;
	std::cout << std::endl << "Nr of normals: " << nNr;
	std::cout << std::endl << "Is rigged: " << (isRigged ? "Yes" : "No");
	std::cout << std::endl << "Nr of joints: " << boneNr;
	std::cout << std::endl << "Nr of materials: " << matNr;
	std::cout << std::endl;

	// ------------------------------------------------------------
	// ---- Init various arrays and variables relevent to them ----
	// ------------------------------------------------------------

	// A mesh file contains both a vertex buffer and an index buffer so that the vertices can be reused. Large amounts of the code exists just to make sure the indexing
	// is correct. The code that create the buffers can be divided into two major steps, first all the data is saved into arrays such ass allPos, allTexCoord, and so on.
	// Then all the vertices are looped through and a "vertCombo" is created for each one, the vertCombo are indices for the data arrays (allPos and ect.) The vertCombo
	// is then check if it is unique or has already been added to the vertexbuffer (the vector "finalVerts").

	// All the possible positions, uvs, and normals are put into arrays that are later accessed with indices
	float3* allPos = new float3[posNr];
	float2* allTexCoord = new float2[uvNr];
	float3* allNorm = new float3[nNr];
	jointConnections* allJntConnections = nullptr;
	int3* allVertCombos = new int3[vertNr];
	if (isRigged)
		allJntConnections = new jointConnections[posNr];
	std::vector<int> allNormIndices;
	allNormIndices.reserve((int)(nNr * 0.6));

	int nrOfPos = 0;
	int nrOfUvs = 0;
	int nrOfNrms = 0;

	// comboEnds is used when all the vertices are being indexed, it
	// logs the uv index, the normal index and the global vert index
	std::vector<int3> comboEnds;
	comboEnds.reserve(posNr * 8);
	int nrOfComboEnds = 0;

	// The way the vertex indexing is done is a little complex, but here is my attempt at an explanation:

	// A comboEnd is the combination of uv index and normal index that correspond to a position 
	// (the last number in a comboEnd is the global unique vertex index)
	// The purpose of this all is to log every position's (aka every controlpoint's) comboEnd so that when we are on the next 
	// vertex we can check if it is unique or not by using it's position index to find all comboEnds for that pos and compare them with the 
	// current comboEnd. That is much faster than comparing the entire combination of indices with all previously logged indices,
	// because that takes exponentially more and more time.

	// The vector comboEndIdxList stores comboIdxLists, they are structs that contain a vector. So comboEndIdxList is a vector that 
	// stores more vectors.
	// There is one entry in comboEndIdxList per position in the mesh, and position indices are used to access a comboIdxList that
	// corresponds to a position. The comboIdxList is a collection indices to all the comboEnds that are logged for that position

	std::vector<comboIdxList> comboEndIdxList;
	comboEndIdxList.reserve(posNr);
	for (int i = 0; i < posNr; i++)
	{
		comboEndIdxList.push_back(comboIdxList());
	}

	// This is a vector for unique vertices, this becomes the vertex buffer in an engine 
	// and the struct "LRM_VERTEX" needs to look the same as in the engine.
	std::vector<LRM_VERTEX> finalVerts;
	// This is a vector for their indices, three indices for every polygon.
	std::vector<std::uint32_t> indices;
	// This is a vector that is used the same as finalVerts, if the mesh is rigged (isRigged == true), this will be used instead of finalVerts. 
	// finalVerts will then go unused and vice versa.
	std::vector<LRSM_VERTEX> finalSkelVerts;
	// This is an array for all the joints
	Joint* finalJoints = nullptr;

	// This is a vecor of int vectors, it is only used when the mesh has multiple materials, there is an extra step where all the vectors (that are sorted depending on material)
	// are all put into the "indices".
	std::vector<std::vector<std::uint32_t>> materialSortedIndices;
	std::vector<std::uint32_t> materialDrawCallOffsets;

	if (!oneMaterial)
	{
		materialSortedIndices.reserve(matNr);
		for (int i = 0; i < matNr; i++)
			materialSortedIndices.push_back(std::vector<std::uint32_t>());

		for (int i = 0; i < matNr; i++)
			materialSortedIndices.at(i).reserve(vertNr / matNr);
	}

	if (!isRigged)
		finalVerts.reserve(vertNr);
	else
	{
		finalSkelVerts.reserve(vertNr);
		finalJoints = new Joint[boneNr];
	}


	indices.reserve(vertNr);

	std::uint32_t highestIndex = 0;

	if (hasNoUvs)
	{
		allTexCoord[0] = (float2{ 0.0f,0.0f });
		allTexCoord[1] = (float2{ 0.0f,1.0f });
		allTexCoord[2] = (float2{ 1.0f,1.0f });
	}

	std::cout << std::endl << "Starting convertion..." << std::endl;

	// -----------------------------------------------
	// ---- Get all positions (aka controlpoints) ----
	// -----------------------------------------------
	FbxVector4* lControlPoints = mesh->GetControlPoints();
	for (int i = 0; i < posNr; i++)
	{
		// All the possible positions are put into an array that is later accessed with indices
		// (In this case the the array is a little unnecessary because it is identical to FbxVector4* lControlPoints, the array could be replaced)
		allPos[nrOfPos] = lControlPoints[i];		   // Get control point positions
		if (flipAllX)
			allPos[nrOfPos].f[0] *= -1;

		nrOfPos++;
	}

	// ---------------------
	// ---- Get all uvs ----
	// ---------------------

	// Here is an explaination of "mapping modes" and "reference modes" in fbx:
	// Mapping modes: Data is stored in various ways in an FBX. the mapping modes that are relevant are: eByControlPoint and eByPolygonVertex
	// eByControlPoint means there is one piece of data (for instance a vector2 of uv coordinates) per controlpoint
	// eByPolygonVertex means there can be more than one per controlpoint and they are accessed by vertexID (the 
	// number you get if you loop though every polygon and every vertex in it)
	// Reference modes: The relevant reference modes are eDirect and eIndexToDirect. eDirect means the data is stored unindexed in the "direct array",
	// eIndexToDirect means that it is indexed and you can find the indicies in a seperate array. 
	// You'll see many switch cases that that account for all this.

	switch (leUV->GetMappingMode())
	{
	case FbxGeometryElement::eByControlPoint:
		switch (leUV->GetReferenceMode())
		{
		case FbxGeometryElement::eDirect:
		{
			for (int i = 0; i < posNr; i++)
			{
				allTexCoord[nrOfUvs++] = leUV->GetDirectArray().GetAt(i); // Get the uvs
				if (flipTexCoordU)
					allTexCoord[nrOfUvs - 1].f[0] = 1 - allTexCoord[nrOfUvs - 1].f[0]; // Flip them if necessary
				if (flipTexCoordV)
					allTexCoord[nrOfUvs - 1].f[1] = 1 - allTexCoord[nrOfUvs - 1].f[1];
			}
			break;
		}
		case FbxGeometryElement::eIndexToDirect:
		{
			for (int i = 0; i < posNr; i++)
			{
				int id = leUV->GetIndexArray().GetAt(i);
				allTexCoord[nrOfUvs++] = leUV->GetDirectArray().GetAt(id); // Get the uvs
				if (flipTexCoordU)
					allTexCoord[nrOfUvs - 1].f[0] = 1 - allTexCoord[nrOfUvs - 1].f[0]; // Flip them if necessary
				if (flipTexCoordV)
					allTexCoord[nrOfUvs - 1].f[1] = 1 - allTexCoord[nrOfUvs - 1].f[1];
			}
			break;
		}
		}
	case FbxGeometryElement::eByPolygonVertex:
	{
		for (int i = 0; i < uvNr; i++)
		{
			allTexCoord[nrOfUvs++] = leUV->GetDirectArray().GetAt(i); // Get the uvs
			if (flipTexCoordU)
				allTexCoord[nrOfUvs - 1].f[0] = 1 - allTexCoord[nrOfUvs - 1].f[0]; // Flip them if necessary
			if (flipTexCoordV)
				allTexCoord[nrOfUvs - 1].f[1] = 1 - allTexCoord[nrOfUvs - 1].f[1];
		}
		break;
	}
	}

	// -------------------------
	// ---- Get all normals ----
	// -------------------------

	// Debug printing of the mapping and refrence modes of the normals
	/*std::cout << "Name: " << (std::string)mesh->GetName() << std::endl;

	std::cout << "Normals mapping mode: ";
	switch (leNormal->GetMappingMode())
	{
	case FbxGeometryElement::eByPolygonVertex:
		std::cout << "eByPolygonVertex" << std::endl;
		break;
	case FbxGeometryElement::eByControlPoint:
		std::cout << "eByControlPoint" << std::endl;
		break;
	default:
		std::cout << "??????" << std::endl;
		break; // other reference modes not shown here!
	}
	std::cout << "Normals reference mode: ";
	switch (leNormal->GetReferenceMode())
	{
	case FbxGeometryElement::eDirect:
		std::cout << "eDirect" << std::endl;
		break;
	case FbxGeometryElement::eIndexToDirect:
		std::cout << "eIndexToDirect" << std::endl;
		break;
	default:
		std::cout << "??????" << std::endl;
		break; // other reference modes not shown here!
	}
	// And now tangents
	std::cout << std::endl << "Tangents mapping mode: ";
	switch (leTangent->GetMappingMode())
	{
	case FbxGeometryElement::eByPolygonVertex:
		std::cout << "eByPolygonVertex" << std::endl;
		break;
	case FbxGeometryElement::eByControlPoint:
		std::cout << "eByControlPoint" << std::endl;
		break;
	default:
		std::cout << "??????" << std::endl;
		break; // other reference modes not shown here!
	}
	std::cout << "Tangents reference mode: ";
	switch (leTangent->GetReferenceMode())
	{
	case FbxGeometryElement::eDirect:
		std::cout << "eDirect" << std::endl;
		break;
	case FbxGeometryElement::eIndexToDirect:
		std::cout << "eIndexToDirect" << std::endl;
		break;
	default:
		std::cout << "??????" << std::endl;
		break; // other reference modes not shown here!
	}
	getchar();*/

	switch (leNormal->GetMappingMode())
	{
	case FbxGeometryElement::eByControlPoint:

		switch (leNormal->GetReferenceMode())
		{
		case FbxGeometryElement::eDirect:
		{
			for (int i = 0; i < posNr; i++)
			{
				allNorm[nrOfNrms] = leNormal->GetDirectArray().GetAt(i); // Get the normals

				nrOfNrms++;
			}
			break;
		}
		case FbxGeometryElement::eIndexToDirect:
		{
			for (int i = 0; i < posNr; i++)
			{
				int id = leNormal->GetIndexArray().GetAt(i);
				allNorm[nrOfNrms++] = leNormal->GetDirectArray().GetAt(id); // Get the normals
			}
			break;
		}
		}
		break;

	case FbxGeometryElement::eByPolygonVertex:

		switch (leNormal->GetReferenceMode())
		{
		case FbxGeometryElement::eDirect:
		{

			//std::cout << "Normals: eDirect" << std::endl;

			// This is the most common way I've noticed for normals to be stored, it is unindexed and very unoptimised so the following code is
			// a quick indexing of all the normals. Not every normal is checked for uniqueness, that would take a long time, so just the last
			// nine (const int backJumps = 9;) is checked since identical normals seemed to follow closly after each other.

			// -Here starts a manual quickfix indexing of the normals-
			int highestNormIndex = 0;
			const int backJumps = 9;
			std::vector<int> recentIndices;
			for (int i = 0; i < backJumps; i++)
				recentIndices.push_back(-1);

			for (int n = 0; n < nNr; n++)
			{
				bool matchFound = false;
				int foundIndex = -1;

				int b;
				for (b = 1; b <= backJumps && !matchFound; b++)
				{
					if (n - b < 0)
						break;

					if (leNormal->GetDirectArray().GetAt(n) == leNormal->GetDirectArray().GetAt(n - b))
					{
						//Match found
						matchFound = true;
						foundIndex = n - b;
						break;
					}
				}

				if (matchFound)
				{
					allNormIndices.push_back(recentIndices.at(backJumps - b));
					recentIndices.push_back(recentIndices.at(backJumps - b));
					recentIndices.erase(recentIndices.begin());
				}
				else
				{
					allNorm[nrOfNrms] = leNormal->GetDirectArray().GetAt(n);
					nrOfNrms++;
					allNormIndices.push_back(highestNormIndex);
					recentIndices.erase(recentIndices.begin());
					recentIndices.push_back(highestNormIndex);

					highestNormIndex++;
				}

				//Debug prints
				/*
				std::cout << "allNormIndices" << std::endl;
				for (int de = 0; de < allNormIndices.size(); de++)
				{
					std::cout << allNormIndices.at(de) << std::endl;
				}
				std::cout << "allNorm" << std::endl;
				for (int de = 0; de < nrOfNrms; de++)
				{
					std::cout << allNorm[de].print() << std::endl;
				}
				std::cout << "recentIndices" << std::endl;
				for (int de = 0; de < recentIndices.size(); de++)
				{
					std::cout << recentIndices.at(de) << std::endl;
				}
				std::cout << "Match found: " << matchFound << std::endl;
				std::cout << "Amount checked: " << n+1 << std::endl;
				std::cout << std::endl;
				*/

			}
			//Debug print
			/*
			bool foundError = false;
			for (int i = 0; i < nNr; i++)
			{
				float3 n;
				n = leNormal->GetDirectArray().GetAt(i);
				std::cout << n.print("D: ") << std::endl;

				if (!(n == allNorm[allNormIndices.at(i)]))
				{
					foundError = true;

					float3 n1 = allNorm[allNormIndices.at(i)];
					std::cout << "ERROR HERE" << std::endl;
				}

				n = allNorm[allNormIndices.at(i)];
				std::cout << n.print("I: ") << std::endl;
				std::cout << std::endl;
			}
			if(foundError)
				std::cout << std::endl << "FOUND AN ERROR!" << std::endl;
			else
				std::cout << std::endl << "No errors :)" << std::endl;

			getchar();
			*/
			/*
			// If you want to run the program without the manual normal indexing, comment out everything else in case FbxGeometryElement::eDirect
			// And add in this for loop
			for (int i = 0; i < nNr; i++)
			{
				allNorm[nrOfNrms++] = leNormal->GetDirectArray().GetAt(i);
				allNormIndices.push_back(i);
			}
			*/
			break;
		}
		case FbxGeometryElement::eIndexToDirect: // I really wish this was the default from Maya's exporter, it cuts all the manual indexing above.
		{
			//std::cout << "Normals: eIndexToDirect" << std::endl;
			for (int i = 0; i < nNr; i++)
			{
				int id = leNormal->GetIndexArray().GetAt(i);
				allNorm[nrOfNrms] = leNormal->GetDirectArray().GetAt(id); // Get the normals
				nrOfNrms++;
			}
			break;
		}
		break;
		}
	}

	if (flipAllX)
	{
		for (int i = 0; i < nNr; i++)
		{
			allNorm[i].f[0] *= -1;
		}
	}
	// -------------------------------------------
	// ---- Calculate tangents and bitangents ----
	// -------------------------------------------

	// http://foundationsofgameenginedev.com/FGED2-sample.pdf
	// https://answers.unity.com/questions/7789/calculating-tangents-vector4.html

	float3* polyVertTang = new float3[vertNr];
	float3* polyVertBiTang = new float3[vertNr];
	// These are deleted after the vert and index buffers are done

	// they are per polygon so don't worry about quads, just loop through all polygons and save the values.
	int vertexId1 = 0;
	int vertexId2 = 0;
	for (int poly = 0; poly < polygonNr; poly++) // Loop for each polygon
	{
		int vertCount = mesh->GetPolygonSize(poly);

		if (vertCount > 4) // Crash if the polygon is not a tri or a quad
			assert(false);

		float3 triPos[3];
		float2 triUv[3];
		
		for (int vertLoop = 0; vertLoop < vertCount; vertLoop++) // Loop for each vertex in the polygon
		{
			// If the order needs to be flipped, some values are adjusted
			int vertexId;
			int vert;
			if (!flipVertOrder)
			{
				vertexId = vertexId1;
				vert = vertLoop;
			}
			else
			{
				int highestVertIndex = vertCount == 3 ? 2 : 3;

				vertexId = vertexId1 - vertLoop + (highestVertIndex - vertLoop);
				vert = highestVertIndex - vertLoop;
			}

			// Time to fill an int3 (vertCombo) with the indices of the vertex

			// Get pos index (aka controlPointIndex)
			int controlPointIndex = mesh->GetPolygonVertex(poly, vert);
			allVertCombos[vertexId1].f[0] = controlPointIndex;

			// Get uv index
			switch (leUV->GetMappingMode())
			{
			case FbxGeometryElement::eByControlPoint:
				switch (leUV->GetReferenceMode())
				{
				case FbxGeometryElement::eDirect:
					allVertCombos[vertexId1].f[1] = allVertCombos[vertexId1].f[0];
					break;
				case FbxGeometryElement::eIndexToDirect:
					allVertCombos[vertexId1].f[1] = leUV->GetIndexArray().GetAt(allVertCombos[vertexId1].f[0]);
					break;
				}
				break;

			case FbxGeometryElement::eByPolygonVertex:
				allVertCombos[vertexId1].f[1] = mesh->GetTextureUVIndex(poly, vert);
				break;
			}

			// Get normal index

			switch (leNormal->GetMappingMode())
			{
			case FbxGeometryElement::eByControlPoint:

				switch (leNormal->GetReferenceMode())
				{
				case FbxGeometryElement::eDirect:
					allVertCombos[vertexId1].f[2] = allVertCombos[vertexId1].f[0];
					break;
				case FbxGeometryElement::eIndexToDirect:
					allVertCombos[vertexId1].f[2] = leNormal->GetIndexArray().GetAt(allVertCombos[vertexId1].f[0]);
					break;
				}

				break;

			case FbxGeometryElement::eByPolygonVertex:

				switch (leNormal->GetReferenceMode())
				{
				case FbxGeometryElement::eDirect:
					allVertCombos[vertexId1].f[2] = allNormIndices.at(vertexId);
					break;
				case FbxGeometryElement::eIndexToDirect:
					allVertCombos[vertexId1].f[2] = leNormal->GetIndexArray().GetAt(vertexId);
					break;
				}
				break;
			}

			// Now that the vertCombos have been covered it's time to calculate the tangents

			
			//const Point3D& p0 = vertexArray[i0];
			//const Point3D& p1 = vertexArray[i1];
			//const Point3D& p2 = vertexArray[i2];
			if(vert < 3)
				triPos[vert] = allPos[allVertCombos[vertexId1].f[0]];
			
			//const Point2D& w0 = texcoordArray[i0];
			//const Point2D& w1 = texcoordArray[i1];
			//const Point2D& w2 = texcoordArray[i2];
			if (vert < 3)
				triUv[vert] = allTexCoord[allVertCombos[vertexId1].f[1]];

			polyVertTang[vertexId1]   = float3( 0,0,0 );
			polyVertBiTang[vertexId1] = float3( 0,0,0 );

			vertexId1++;
		}
		//int32 i0 = triangleArray[k].index[0];
		//int32 i1 = triangleArray[k].index[1];
		//int32 i2 = triangleArray[k].index[2];

		float3 e1 = triPos[1] - triPos[0];
		float3 e2 = triPos[2] - triPos[0];
		float x1 = triUv[1].f[0] - triUv[0].f[0];
		float x2 = triUv[2].f[0] - triUv[0].f[0];
		float y1 = triUv[1].f[1] - triUv[0].f[1];
		float y2 = triUv[2].f[1] - triUv[0].f[1];
		float r = 1.0f / (x1 * y2 - x2 * y1);
		float3 t = (e1 * y2 - e2 * y1) * r;
		float3 b = (e2 * x1 - e1 * x2) * r;

		// loop thro the polygon
		for (int vertLoop = 0; vertLoop < vertCount; vertLoop++) // Loop for each vertex in the polygon
		{
			polyVertTang[vertexId2]   = polyVertTang[vertexId2]   + t;
			polyVertBiTang[vertexId2] = polyVertBiTang[vertexId2] + b;
			vertexId2++;
		}
	}

	// loop thro all normals
	//for (int nrm = 0; nrm < nNr; nrm++)
	//{
	//	allNorm[nrm];	// I was working on how to actually access the tangents and binormals here, or if i need to rethink something
	//	Vector3 t = tan1[a];
	//	Vector3 tmp = (t - n * Vector3.Dot(n, t)).normalized;
	//	tangents[a] = new Vector4(tmp.x, tmp.y, tmp.z);
	//	tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) & lt; 0.0f) ? -1.0f : 1.0f;
	//	
	//}

	// ----------------------------
	// ---- Save skeleton data ----
	// ----------------------------

	if (isRigged)
	{
		std::unordered_map<std::string, int> boneIdxs;

		for (int boneIdx = 0; boneIdx < boneNr; boneIdx++)
		{
			FbxCluster* bone = ((FbxSkin*)mesh->GetDeformer(0, FbxDeformer::eSkin))->GetCluster(boneIdx);

			//std::cout << bone->GetLink()->GetName() << " idx: " << boneIdx << std::endl;
			int indexCount = bone->GetControlPointIndicesCount();
			int* lIndices = bone->GetControlPointIndices();
			double* lWeights = bone->GetControlPointWeights();
			for (int k = 0; k < indexCount; k++)
			{
				allJntConnections[lIndices[k]].allJoints.push_back(boneIdx);
				allJntConnections[lIndices[k]].allWeights.push_back(lWeights[k]);
			}
			//std::string n = bone->GetLink()->GetName();
			boneIdxs[bone->GetLink()->GetName()] = boneIdx;
		}

		FbxCluster* rootCluster = ((FbxSkin*)mesh->GetDeformer(0, FbxDeformer::eSkin))->GetCluster(0); // root bone, hopefully.
		FbxNode* rootLink = rootCluster->GetLink();

		// We then check if the root link truly is the root joint by checking if it has a parent and if the parent is a joint as well.
		if (rootLink->GetParent() && rootLink->GetParent()->GetNodeAttribute())
		{
			FbxNodeAttribute::EType parentType = rootLink->GetParent()->GetNodeAttribute()->GetAttributeType();

			if (parentType == FbxNodeAttribute::EType::eSkeleton)
				while (parentType == FbxNodeAttribute::EType::eSkeleton)
				{
					rootLink = rootLink->GetParent();
					parentType = rootLink->GetParent()->GetNodeAttribute()->GetAttributeType();
				}
		}
		rootJointIdx = boneIdxs.at(rootLink->GetName());

		//std::string rootName = rootLink->GetName(); 

		//bool yUp = rootLink->GetScene()->GetGlobalSettings().GetAxisSystem() == FbxAxisSystem::MayaYUp;
		//bool yRightHand = rootLink->GetScene()->GetGlobalSettings().GetAxisSystem().GetCoorSystem() == FbxAxisSystem::eRightHanded;
		//bool yunits = rootLink->GetScene()->GetGlobalSettings().GetSystemUnit() == FbxSystemUnit::cm;

		getAllJoints(&finalJoints, &boneIdxs, rootLink);

		if (flipAllX)
		{
			for (int i = 0; i < boneNr; i++)
			{
				//finalJoints->translation.f[0] *= -1;
				finalJoints->rotation.f[0] *= -1;
			}
		}

		// Loop through all jntConnections. Take the alljoints and weights vectors and put them into the small arrays.
		// Take the four highest, normalize if there were more than four.
		for (int i = 0; i < posNr; i++)
		{
			int totalJoints = allJntConnections[i].allJoints.size();

			if (totalJoints <= 4)
			{
				for (int u = 0; u < totalJoints; u++)
				{
					allJntConnections[i].finalJoints[u] = allJntConnections[i].allJoints.at(u);
					allJntConnections[i].finalWeights[u] = allJntConnections[i].allWeights.at(u);
				}

			}
			else // if more than four joints were assigned to the vertex
			{
				double adage = 0.0;
				// Loop thro and find the highest of them all, add it to the array, remove from the alljoints vector.
				for (int u = 0; u < 4; u++)
				{
					int idxOfHighest = 0;
					double highestWeight = 0.0;
					for (int jnt = 0; jnt < allJntConnections[i].allJoints.size(); jnt++)
					{
						if (allJntConnections[i].allWeights.at(jnt) > highestWeight)
						{
							highestWeight = allJntConnections[i].allWeights.at(jnt);
							idxOfHighest = jnt;
						}
					}
					allJntConnections[i].finalJoints[u] = allJntConnections[i].allJoints.at(idxOfHighest);
					allJntConnections[i].finalWeights[u] = allJntConnections[i].allWeights.at(idxOfHighest);
					adage += allJntConnections[i].allWeights.at(idxOfHighest);
					allJntConnections[i].allJoints.erase(allJntConnections[i].allJoints.begin() + idxOfHighest);
					allJntConnections[i].allWeights.erase(allJntConnections[i].allWeights.begin() + idxOfHighest);
				}
				for (int u = 0; u < 4; u++)
					allJntConnections[i].finalWeights[u] = allJntConnections[i].finalWeights[u] / adage;

				// These are slight corrections to adage, they are so insignificant that they more than likely will never be needed. but I'm leaving them in anyway.
				double newAdage = 0.0;
				for (int u = 0; u < 4; u++)
					newAdage += allJntConnections[i].finalWeights[u];

				if (newAdage != 1.0)
					allJntConnections[i].finalWeights[3] += 1.0 - newAdage;

				newAdage = 0.0;
				for (int u = 0; u < 4; u++)
					newAdage += allJntConnections[i].finalWeights[u];
			}
		}
		// Debug prints
		/*for (int i = 0; i < boneNr; i++)
		{
			std::cout << " idx: " << finalJoints[i].index << std::endl;
			for (std::pair<std::string, int> element : boneIdxs)
			{
				if (element.second == finalJoints[i].index)
				{
					std::cout << " name: " << element.first << std::endl;
					break;
				}
			}
			std::cout << " Translation: " << finalJoints[i].translation.print() << std::endl;
			std::cout << " Rotation: " << finalJoints[i].rotation.print() << std::endl;
			std::cout << "           children: ";
			for (int u = 0; u < finalJoints[i].nrOfChildren; u++)
			{
				std::cout << finalJoints[i].children[u] << ", ";
			}
			std::cout << std::endl ;
		}*/
		/*std::cout << "Joint connections" << std::endl;
		for (int i = 0; i < posNr; i++)
		{
			double adage = 0.0f;
			std::cout << "Jointinfo for vertex nr " << i << ": " << std::endl;
			for (int u = 0; u < 4; u++)
			{

				if (allJntConnections[i].finalJoints[u] != -1)
				{
					std::cout << "   joint:  " << allJntConnections[i].finalJoints[u] << std::endl;
					std::cout << "   weight: " << allJntConnections[i].finalWeights[u] << std::endl;
					adage += allJntConnections[i].finalWeights[u];
				}
				else
				{
					std::cout << "   none" << std::endl;
					std::cout << "   none" << std::endl;
				}
			}
			std::cout << "		adage: " << adage << std::endl;
			if (adage > 1.0)
			{
				std::cout << "	TOO MUCH ADAGE" << std::endl;
			}
		}*/

		// change all the -1s into 0s
		for (int i = 0; i < posNr; i++)
		{
			for (int u = 0; u < 4; u++)
			{
				if (allJntConnections[i].finalJoints[u] == -1)
				{
					allJntConnections[i].finalJoints[u] = 0;
					allJntConnections[i].finalWeights[u] = 0.f;
				}
			}
		}

		// --------------------------------------
		// ---- Read and save animation data ----
		// --------------------------------------

		bool hasAnimation = false;

		std::vector<Animation> animations;

		int nrOfAnimStacks = rootLink->GetScene()->GetSrcObjectCount<FbxAnimStack>();

		for (int i = 0; i < nrOfAnimStacks; i++)
		{
			FbxAnimStack* animStack = rootLink->GetScene()->GetSrcObject<FbxAnimStack>(i);

			rootLink->GetScene()->SetCurrentAnimationStack(animStack);

			int nrOfAnimLayers = animStack->GetMemberCount<FbxAnimLayer>();

			// Unused but maybe useful variable, needs testing
			//double frameRateTest = FbxTime::GetFrameRate( rootLink->GetScene()->GetGlobalSettings().GetTimeMode() );

			//fbxsdk::FbxTimeSpan timeSpan = animStack->GetLocalTimeSpan();
			//double animStackStopTime = timeSpan.GetStop().GetSecondDouble();

			for (int u = 0; u < nrOfAnimLayers; u++)
			{
				bool rootHasAnimationInterval;

				fbxsdk::FbxTimeSpan layerTimeSpan;
				rootHasAnimationInterval = rootLink->GetAnimationInterval(layerTimeSpan, animStack, u);

				if (rootHasAnimationInterval)
				{
					hasAnimation = true;

					double animLayerStartTime = layerTimeSpan.GetStart().GetSecondDouble();
					double animLayerStopTime = layerTimeSpan.GetStop().GetSecondDouble();

					Animation currentAnimation;

					currentAnimation.timeSpan = (float)animLayerStopTime - (float)animLayerStartTime;

					//getAnimationFrames(&currentAnimation, rootLink, animLayerStopTime, 30.0);

					FbxAnimEvaluator* eval = rootLink->GetAnimationEvaluator();

					int counter1 = 0;
					fbxsdk::FbxTime frameTime;
					double frameRate = 30.0;

					currentAnimation.frames.reserve((int)frameRate * currentAnimation.timeSpan);

					for (double f = animLayerStartTime; f <= animLayerStopTime; f += (1.0 / frameRate))
					{
						frameTime.SetSecondDouble(f);

						AnimationFrame currentFrame;

						currentFrame.timeStamp = f;

						currentFrame.jointTransforms = new JointTransformValues[boneNr];
						//currentFrame.jointTransforms.reserve(boneNr);

						getAnimationFrames(&currentFrame, &boneIdxs, rootLink, &frameTime, eval);

						if (flipAllX)
						{
							for (int b = 0; b < boneNr; b++)
							{
								//currentFrame.jointTransforms[b].translation.f[0] *= -1;
								//currentFrame.jointTransforms[b].rotationQuat.f[0] *= -1;
								//currentFrame.jointTransforms[b].rotationQuat.f[3] *= -1;
								float temp = currentFrame.jointTransforms[b].rotationQuat.f[1];
								currentFrame.jointTransforms[b].rotationQuat.f[1] = currentFrame.jointTransforms[b].rotationQuat.f[2];
								currentFrame.jointTransforms[b].rotationQuat.f[2] = temp;
							}
						}

						currentAnimation.frames.push_back(currentFrame);

						counter1++;
					}

					animations.push_back(currentAnimation);
				}
				else
				{	// check if it has just one pose
					if (animStack->GetMemberCount<FbxAnimLayer>() > 0)
					{
						FbxAnimLayer* animLayer = animStack->GetMember<FbxAnimLayer>(u);

						FbxAnimCurve* animCurve = rootLink->LclTranslation.GetCurve(animLayer, FBXSDK_CURVENODE_COMPONENT_X);
						//animCurve->GetTimeInterval()
						if (animCurve && animCurve->KeyGetCount() == 1)
						{
							hasAnimation = true;

							Animation currentAnimation;

							AnimationFrame currentFrame;

							currentFrame.timeStamp = 0.0f;

							currentFrame.jointTransforms = new JointTransformValues[boneNr];

							FbxAnimEvaluator* eval = rootLink->GetAnimationEvaluator();
							fbxsdk::FbxTime frameTime;
							frameTime.SetSecondDouble(0.0f);
							getAnimationFrames(&currentFrame, &boneIdxs, rootLink, &frameTime, eval);

							if (flipAllX)
							{
								for (int b = 0; b < boneNr; b++)
								{
									//currentFrame.jointTransforms[b].translation.f[0] *= -1;
									//currentFrame.jointTransforms[b].rotationQuat.f[0] *= -1;
									//currentFrame.jointTransforms[b].rotationQuat.f[3] *= -1;
									float temp = currentFrame.jointTransforms[b].rotationQuat.f[1];
									currentFrame.jointTransforms[b].rotationQuat.f[1] = currentFrame.jointTransforms[b].rotationQuat.f[2];
									currentFrame.jointTransforms[b].rotationQuat.f[2] = temp;
								}
							}

							currentAnimation.frames.push_back(currentFrame);
							animations.push_back(currentAnimation);
						}
					}
				}
			}
		}
		/*
		if (!animations.empty())
		{
			int diffCount = 0;
			int c = 0;

			for (int i = 0; i < animations.at(0).frames.size(); i++)
			{
				for (int u = 0; u < boneNr; u++)
				{
					c++;
					if (u != rootJointIdx && !(animations.at(0).frames.at(i).jointTransforms[u].translation == animations.at(0).frames.at(0).jointTransforms[u].translation))
						diffCount++;
				}
			}
			int aeghazeg = 0;
		}
		*/

		// -------------------------------------
		// ---- Write the animation file(s) ----
		// -------------------------------------

		// Build a filename
		size_t indexBeforeFilename = filename.find_last_of("/") + 1;
		std::string endOfFilename = filename.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename); // endOfFilename is the name of the fbx but without .fbx and the path

		for (int i = 0; i < animations.size(); i++)
		{
			std::string FinalFilename;

			std::string fileType = ".lra";

			if (i > 0)
				endOfFilename.append(std::to_string(i));

			std::string destinationPath = "../../../DuplexEngine/StortSpel/res/animations/";

			FinalFilename = destinationPath + endOfFilename + fileType;

			//std::cout << "destinationPath:	" << destinationPath << std::endl;
			//std::cout << "endOfFilename:	" << endOfFilename << std::endl << std::endl;

			std::ofstream myFile(FinalFilename, std::ios::out | std::ios::binary);

			std::uint32_t frameCount = (std::uint32_t)animations.at(i).frames.size();
			AnimationFrame* AnimationFrameArray = &animations.at(i).frames[0];

			char* animData = new char[(sizeof(float) + sizeof(JointTransformValues) * boneNr) * frameCount];
			int offset = 0;
			for (int u = 0; u < animations.at(i).frames.size(); u++)
			{
				memcpy(animData + offset, &animations.at(i).frames.at(u).timeStamp, sizeof(float));

				offset += sizeof(float);

				for (int b = 0; b < boneNr; b++)
				{
					memcpy(animData + offset, &animations.at(i).frames.at(u).jointTransforms[b], sizeof(JointTransformValues));

					offset += sizeof(JointTransformValues);
				}

			}

			myFile.write((char*)&animations.at(i).timeSpan, sizeof(float));	// timespan

			myFile.write((char*)&frameCount, sizeof(std::uint32_t));		// frameCount

			myFile.write((char*)&boneNr, sizeof(std::uint32_t));			// jointCount

			myFile.write(animData, (sizeof(float) + sizeof(JointTransformValues) * boneNr) * frameCount);

			myFile.close();
		}
	}

	// --------------------------------------------------
	// ---- Build the vertex buffer and index buffer ----
	// --------------------------------------------------

	/*for (int delLater = 0; delLater < vertNr; delLater++)
	{
		std::cout << "bluh:	" << allVertCombos[delLater].print() << std::endl;
	}*/
	vertexId1 = 0;
	for (int poly = 0; poly < polygonNr; poly++) // Loop for each polygon
	{
		int vertCount = mesh->GetPolygonSize(poly);

		std::uint32_t addedIndices[4];

		int matId = 0;

		std::vector<std::uint32_t>* indicesVectorPtr;

		if (!oneMaterial)
		{
			FbxGeometryElementMaterial* lMaterialElement = mesh->GetElementMaterial(0);
			matId = lMaterialElement->GetIndexArray().GetAt(poly);

			indicesVectorPtr = &materialSortedIndices.at(matId);
		}
		else
		{
			indicesVectorPtr = &indices;
		}

		for (int vertLoop = 0; vertLoop < vertCount; vertLoop++) // Loop for each vertex in the polygon
		{
			// If the order needs to be flipped, some values are adjusted
			int vert;
			if (!flipVertOrder)
			{
				vert = vertLoop;
			}
			else
			{
				int highestVertIndex = vertCount == 3 ? 2 : 3;

				vert = highestVertIndex - vertLoop;
			}

			// Unique-vert checking code starts here, this is the old algoritm for indexing

			int dupPlace = -1;

			bool uniqueVert = false;
			int posIdx = allVertCombos[vertexId1].f[0];

			if (comboEndIdxList.at(posIdx).nrOfcomboIdxs == 0)
			{ //checks that there is no comboendidx for this allPos idx, so the vert is unique

				uniqueVert = true;
				comboEndIdxList.at(posIdx).comboIdx.push_back(nrOfComboEnds++);
				comboEndIdxList.at(posIdx).nrOfcomboIdxs += 1;
				comboEnds.push_back(int3{ allVertCombos[vertexId1].f[1], allVertCombos[vertexId1].f[2], (int)highestIndex });
			}
			else
			{
				bool loopBreak = false;
				for (int comb = 0; comb < comboEndIdxList.at(posIdx).comboIdx.size() && !loopBreak; comb++)
				{
					if (comboEnds.at(comboEndIdxList.at(posIdx).comboIdx.at(comb)).f[0] == allVertCombos[vertexId1].f[1] && comboEnds.at(comboEndIdxList.at(posIdx).comboIdx.at(comb)).f[1] == allVertCombos[vertexId1].f[2])
					{
						// if true the vertex is nonunique
						// so get the index from the comboEnd array, add it to the indices array and break the loop

						addedIndices[vert] = comboEnds.at(comboEndIdxList.at(posIdx).comboIdx.at(comb)).f[2];
						(*indicesVectorPtr).push_back(addedIndices[vert]);
						loopBreak = true;
						break;
					}
				}
				if (loopBreak == false)
				{
					// if every comboEndIndex was checked and non were -1 or right just write a unique vert
					uniqueVert = true;
				}
			}

			if (uniqueVert)
			{
				// Just gonna put the tangent and binormal calculation here, don't mind me.
				
				// n = allNorm[allVertCombos[vertexId1].f[2]];
				// t = polyVertTang[vertexId1];
				float3 tan = (polyVertTang[vertexId1] - allNorm[allVertCombos[vertexId1].f[2]] * float3::dot(allNorm[allVertCombos[vertexId1].f[2]], polyVertTang[vertexId1]));
				//tan = polyVertTang[vertexId1];
				tan.normalize();
				float3 biTan = float3::cross(allNorm[allVertCombos[vertexId1].f[2]], polyVertTang[vertexId1]);
				//float3 biTan = float3::cross(polyVertTang[vertexId1], allNorm[normalIndex]);
				biTan.normalize();
				
				float handedNess = (float3::dot(float3::cross(allNorm[allVertCombos[vertexId1].f[2]], polyVertTang[vertexId1]), polyVertBiTang[vertexId1]) < 0.0f) ? -1.0f : 1.0f;
				biTan = biTan * handedNess;
				//Vector3 t = tan1[a];
				//Vector3 tmp = (t - n * Vector3.Dot(n, t)).normalized;
				//tangents[a] = new Vector4(tmp.x, tmp.y, tmp.z);
				
				// Add the unique vertex to the vertexbuffer

				//std::cout << "Vert nr: " << highestIndex << vertCombo.print(": ") << std::endl;
				
				if (!isRigged)
				{
					LRM_VERTEX vertex;
					vertex =
					{
						allPos[allVertCombos[vertexId1].f[0]],
						allTexCoord[allVertCombos[vertexId1].f[1]],
						allNorm[allVertCombos[vertexId1].f[2]],
						tan,
						biTan
						//allTang[allVertCombos[vertexId1].f[2]],
						//allBiTang[allVertCombos[vertexId1].f[2]]
					};
					finalVerts.push_back(vertex);
				}
				else
				{
					LRSM_VERTEX vertex;
					vertex = {
						allPos[allVertCombos[vertexId1].f[0]],
						allTexCoord[allVertCombos[vertexId1].f[1]],
						allNorm[allVertCombos[vertexId1].f[2]],
						tan,
						biTan,
						//allTang[allVertCombos[vertexId1].f[2]],
						//allBiTang[allVertCombos[vertexId1].f[2]],
						allJntConnections[allVertCombos[vertexId1].f[0]].finalJoints,
						allJntConnections[allVertCombos[vertexId1].f[0]].finalWeights
					};
					finalSkelVerts.push_back(vertex);
				}

				(*indicesVectorPtr).push_back(highestIndex);

				addedIndices[vert] = highestIndex;
				highestIndex++;
			}

			vertexId1++;
		}

		if (vertCount == 4) // Turns out it's this easy to support quads :S
		{
			if (!flipVertOrder) // CCW (i think)
			{
				(*indicesVectorPtr).push_back(addedIndices[0]);
				(*indicesVectorPtr).push_back(addedIndices[2]);
			}
			else // CW (i think)
			{
				(*indicesVectorPtr).push_back(addedIndices[3]);
				(*indicesVectorPtr).push_back(addedIndices[1]);
			}
		}
	}

	delete[] polyVertTang;
	delete[] polyVertBiTang;

	if (!oneMaterial)
	{
		int offset = 0;

		for (int i = 0; i < matNr; i++)
		{
			for (int u = 0; u < materialSortedIndices.at(i).size(); u++)
			{
				indices.push_back(materialSortedIndices.at(i).at(u));
			}

			offset += materialSortedIndices.at(i).size();

			//if(i != matNr - 1)
			materialDrawCallOffsets.push_back(offset);
		}
	}

	// ----------------------------
	// ---- Write the LRM file ----
	// ----------------------------

	// Build a filename
	std::string FinalFilename;
	size_t indexBeforeFilename = filename.find_last_of("/") + 1;
	std::string endOfFilename;
	std::string fileType = isRigged ? ".lrsm" : ".lrm";

	bool noUniqueName = true;

	FbxProperty uniqueNameParam = pNode->FindProperty(std::string("uniqueName").c_str(), false);
	if (uniqueNameParam.IsValid())
	{
		endOfFilename = uniqueNameParam.Get<FbxString>().Buffer(); // If the mesh has an attribute describing a unique name, we use that

		if (endOfFilename != "")
		{
			noUniqueName = false;
		}
	}

	if (noUniqueName)
	{
		endOfFilename = filename.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename); // otherwise we use the name of the fbx but without .fbx and the path

		if (endOfFilename != pNode->GetName()) // If the file and the mesh don't have the same name
			endOfFilename.append("_" + std::string(pNode->GetName())); // We append the mesh's name to the file.
	}

	//std::string destinationPath = "../Models/";
	std::string destinationPath = "../../../DuplexEngine/StortSpel/res/models/";

	FinalFilename = destinationPath + endOfFilename + fileType;

	std::cout << "destinationPath:	" << destinationPath << std::endl;
	std::cout << "endOfFilename:	" << endOfFilename << std::endl << std::endl;

	std::ofstream myFile(FinalFilename, std::ios::out | std::ios::binary);

	std::uint32_t vertCount;
	std::uint32_t idxCount = (std::uint32_t)indices.size();

	if (!isRigged)
	{
		vertCount = (std::uint32_t)finalVerts.size();

		LRM_VERTEX* vertBufferArray = &finalVerts[0];
		std::uint32_t* indexBufferArray = &indices[0];

		// Write the file
		std::cout << "Writing LRM file..." << std::endl;
		myFile.write((char*)&vertCount, sizeof(std::uint32_t));
		std::cout << 1 << "... ";
		myFile.write((char*)&vertBufferArray[0], sizeof(LRM_VERTEX) * vertCount);
		std::cout << 2 << "... ";
		myFile.write((char*)&idxCount, sizeof(std::uint32_t));
		std::cout << 3 << "... ";
		myFile.write((char*)&indexBufferArray[0], sizeof(std::uint32_t) * idxCount);
		std::cout << 4 << "... ";
		myFile.write((char*)&matNr, sizeof(std::uint32_t));
		std::cout << 5 << "... ";
		if (matNr > 1)
		{
			myFile.write((char*)&materialDrawCallOffsets[0], sizeof(std::uint32_t) * materialDrawCallOffsets.size());
			std::cout << 6 << "... ";
		}
	}
	else
	{
		vertCount = (std::uint32_t)finalSkelVerts.size();

		LRSM_VERTEX* vertBufferArray = &finalSkelVerts[0];
		std::uint32_t* indexBufferArray = &indices[0];

		// Write the file
		std::cout << "Writing LRSM file..." << std::endl;
		myFile.write((char*)&vertCount, sizeof(std::uint32_t));
		std::cout << 1 << "... ";
		myFile.write((char*)&vertBufferArray[0], sizeof(LRSM_VERTEX) * vertCount);
		std::cout << 2 << "... ";
		myFile.write((char*)&idxCount, sizeof(std::uint32_t));
		std::cout << 3 << "... ";
		myFile.write((char*)&indexBufferArray[0], sizeof(std::uint32_t) * idxCount);
		std::cout << 4 << "... ";
		myFile.write((char*)&boneNr, sizeof(std::uint32_t));
		std::cout << 5 << "... ";
		myFile.write((char*)&rootJointIdx, sizeof(std::uint32_t));
		std::cout << 6 << "... ";
		myFile.write((char*)&finalJoints[0], sizeof(Joint) * boneNr);
		std::cout << 7 << "... ";
		myFile.write((char*)&matNr, sizeof(std::uint32_t));
		std::cout << 8 << "... ";
		if (matNr > 1)
		{
			myFile.write((char*)&materialDrawCallOffsets[0], sizeof(std::uint32_t) * materialDrawCallOffsets.size());
			std::cout << 9 << "... ";
		}
		//delete allJntConnections;
	}

	myFile.close();
	std::cout << "File Written!" << std::endl << std::endl;

	std::cout << "Final verts size: " << vertCount << std::endl;
	std::cout << "Final index size: " << idxCount << std::endl << std::endl;

	delete[] allPos;
	delete[] allNorm;
	delete[] allTexCoord;

	delete[] allVertCombos;





	// --------------------------------------------------
	// ---- Read and store material and texture data ----
	// --------------------------------------------------

	/*
	DisplayString("\n\n-------------------------------------------------------------------");
	DisplayString("---------------- MATERIAL AND TEXTURE OUTPUT START ----------------");
	DisplayString("-------------------------------------------------------------------\n");

	std::vector<MaterialDescription> materialDescriptions;

	int materialCount = pNode->GetMaterialCount();

	if (materialCount > 0)
	{
		FbxPropertyT<FbxDouble3> lKFbxDouble3;
		FbxPropertyT<FbxDouble> lKFbxDouble1;

		for (int lCount = 0; lCount < materialCount; lCount++)
		{
			materialDescriptions.push_back(MaterialDescription());

			fbxsdk::FbxSurfaceMaterial* lMaterial = pNode->GetMaterial(lCount);

			if (lMaterial->GetClassId().Is(fbxsdk::FbxSurfaceLambert::ClassId))
			{
				//// Material name
				((std::string)lMaterial->GetName()).copy(materialDescriptions.at(lCount).materialName, 50);

				// Ambient Color
				lKFbxDouble3 = ((fbxsdk::FbxSurfaceLambert*)lMaterial)->Ambient;
				for (size_t i = 0; i < 3; i++)
					materialDescriptions.at(lCount).ambient[i] = lKFbxDouble3.Get()[i];

				// Diffuse Color
				lKFbxDouble3 = ((fbxsdk::FbxSurfaceLambert*)lMaterial)->Diffuse;
				for (size_t i = 0; i < 3; i++)
					materialDescriptions.at(lCount).diffuse[i] = lKFbxDouble3.Get()[i];

				// Emissive
				lKFbxDouble3 = ((fbxsdk::FbxSurfaceLambert*)lMaterial)->Emissive;
				for (size_t i = 0; i < 3; i++)
					materialDescriptions.at(lCount).emissive[i] = lKFbxDouble3.Get()[i];

				// Opacity
				lKFbxDouble1 = ((fbxsdk::FbxSurfaceLambert*)lMaterial)->TransparencyFactor;
				materialDescriptions.at(lCount).opacity = 1.0 - lKFbxDouble1.Get();
			}
			else
				DisplayString("Unknown type of Material");
		}
	}

	materialDescriptions;
	int xba = 9;

	// Read texture data

	std::string fullDiffuseTexturePath = "None";
	std::string fullNormalTexturePath = "None";

	FbxMesh* lMesh = (FbxMesh*)pNode->GetNodeAttribute();

	int lMaterialIndex;
	FbxProperty lProperty;
	if (lMesh->GetNode() == NULL)
	{
		DisplayString("ERROR: Mesh was NULL while trying to read texture data");
	}
	else
	{
		int lNbMat = lMesh->GetNode()->GetSrcObjectCount<fbxsdk::FbxSurfaceMaterial>();
		for (lMaterialIndex = 0; lMaterialIndex < lNbMat; lMaterialIndex++)
		{
			fbxsdk::FbxSurfaceMaterial* lMaterial = lMesh->GetNode()->GetSrcObject<fbxsdk::FbxSurfaceMaterial>(lMaterialIndex);
			bool lDisplayHeader = true;

			bool hasDiffuseTexture = false;
			bool hasNormalTexture = false;

			//go through all the possible textures
			if (lMaterial)
			{
				int lTextureIndex;
				FBXSDK_FOR_EACH_TEXTURE(lTextureIndex)
				{
					bool pDisplayHeader = true;

					lProperty = lMaterial->FindProperty(FbxLayerElement::sTextureChannelNames[lTextureIndex]);
					if (lProperty.IsValid())
					{
						int lTextureCount = lProperty.GetSrcObjectCount<FbxTexture>();

						for (int j = 0; j < lTextureCount; ++j)
						{
							//no layered texture simply get on the property
							FbxTexture* lTexture = lProperty.GetSrcObject<FbxTexture>(j);
							if (lTexture)
							{
								FbxFileTexture* lFileTexture = FbxCast<FbxFileTexture>(lTexture);
								FbxProceduralTexture* lProceduralTexture = FbxCast<FbxProceduralTexture>(lTexture);

								// Check whether the texture is a diffuse or bump texture, and store it in the appropriate variable
								if (lProperty.GetName() == "DiffuseColor")
								{
									//fbxsdk::FbxObject
									((std::string)lTexture->GetName()).copy(materialDescriptions.at(lMaterialIndex).diffuseTextureName, 50);
									if (lFileTexture)
									{
										fullDiffuseTexturePath = (char*)lFileTexture->GetFileName();
										hasDiffuseTexture = true;
									}

								}
								else if (lProperty.GetName() == "Bump")
								{
									((std::string)lTexture->GetName()).copy(materialDescriptions.at(lMaterialIndex).normalTextureName, 50);
									if (lFileTexture)
									{
										fullNormalTexturePath = (char*)lFileTexture->GetFileName();
										hasNormalTexture = true;
									}
								}
							}
						}
					}//end if pProperty
				}
			}//end if(lMaterial)

			// Copy the textures to the destination
			if (hasDiffuseTexture)
			{
				indexBeforeFilename = fullDiffuseTexturePath.find_last_of("/") + 1;
				std::string fileNameOnly = fullDiffuseTexturePath.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename);
				copyFile(fullDiffuseTexturePath.c_str(), std::string(destinationPath + fileNameOnly).c_str());

				fileNameOnly.copy(materialDescriptions.at(lMaterialIndex).diffuseTexturePath, 50);
			}
			if (hasNormalTexture)
			{
				indexBeforeFilename = fullNormalTexturePath.find_last_of("/") + 1;
				std::string fileNameOnly = fullNormalTexturePath.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename);
				copyFile(fullNormalTexturePath.c_str(), std::string(destinationPath + fileNameOnly).c_str());

				fileNameOnly.copy(materialDescriptions.at(lMaterialIndex).normalTexturePath, 50);
			}


		}// end for lMaterialIndex
	}

	//for (auto& materialDescription : materialDescriptions)
	//{
	//	std::cout << std::endl << std::endl << "Material Description: Name: " << materialDescriptions.at(0).materialName;

	//	std::cout << std::endl << "Ambient: ";
	//	for (size_t i = 0; i < 3; i++)
	//		std::cout << materialDescriptions.at(0).ambient[i] << "     ";

	//	std::cout << std::endl << "Diffuse: ";
	//	for (size_t i = 0; i < 3; i++)
	//		std::cout << materialDescriptions.at(0).diffuse[i] << "     ";

	//	std::cout << std::endl << "Emissive: ";
	//	for (size_t i = 0; i < 3; i++)
	//		std::cout << materialDescriptions.at(0).emissive[i] << "     ";

	//	std::cout << std::endl << "Opacity: " << materialDescriptions.at(0).opacity;
	//	std::cout << std::endl << "Diffuse Texture Name: " << materialDescriptions.at(0).diffuseTextureName;
	//	std::cout << std::endl << "Diffuse Texture Path: " << materialDescriptions.at(0).diffuseTexturePath;
	//	std::cout << std::endl << "Normal Texture Name: " << materialDescriptions.at(0).normalTextureName;
	//	std::cout << std::endl << "Normal Texture Path: " << materialDescriptions.at(0).normalTexturePath;
	//}

	DisplayString("\n-------------------------------------------------------------------");
	DisplayString("---------------- MATERIAL AND TEXTURE OUTPUT END ----------------");
	DisplayString("-------------------------------------------------------------------\n\n");

	// -------------------------
	// ---- Write lrmat files ----
	// -------------------------

	for (auto& materialDescription : materialDescriptions)
	{
		std::string lrmatFilename;

		lrmatFilename = destinationPath + materialDescription.materialName + ".lrmat";

		std::cout << "Writing lrmat file..." << std::endl;
		std::ofstream lrmatFile(lrmatFilename, std::ios::out | std::ios::binary);

		lrmatFile.write((char*)&materialDescription, sizeof(MaterialDescription));

		lrmatFile.close();
	}

	// Test read

	//for (auto& materialDescription : materialDescriptions)
	//{
	//	MaterialDescription materialDescriptionRead;

	//	std::string lrmatReadFilename = destinationPath + materialDescription.materialName + ".lrmat";

	//	std::cout << std::endl << std::endl << "lrmatReadFilename: " << lrmatReadFilename;

	//	std::ifstream lrmatFileRead(lrmatReadFilename, std::ios::in | std::ios::binary);

	//	if (!lrmatFileRead)
	//	{
	//		lrmatFileRead.clear();
	//		lrmatFileRead.close();
	//		return;
	//	}

	//	lrmatFileRead.read(reinterpret_cast<char*>(&materialDescriptionRead), sizeof(MaterialDescription));

	//	std::cout << std::endl << std::endl << "Material Description: Name: " << materialDescriptionRead.materialName;

	//	std::cout << std::endl << "Ambient: ";
	//	for (size_t i = 0; i < 3; i++)
	//		std::cout << materialDescriptionRead.ambient[i] << "     ";

	//	std::cout << std::endl << "Diffuse: ";
	//	for (size_t i = 0; i < 3; i++)
	//		std::cout << materialDescriptionRead.diffuse[i] << "     ";

	//	std::cout << std::endl << "Emissive: ";
	//	for (size_t i = 0; i < 3; i++)
	//		std::cout << materialDescriptionRead.emissive[i] << "     ";

	//	std::cout << std::endl << "Opacity: " << materialDescriptionRead.opacity;
	//	std::cout << std::endl << "Diffuse Texture Name: " << materialDescriptionRead.diffuseTextureName;
	//	std::cout << std::endl << "Diffuse Texture Path: " << materialDescriptionRead.diffuseTexturePath;
	//	std::cout << std::endl << "Normal Texture Name: " << materialDescriptionRead.normalTextureName;
	//	std::cout << std::endl << "Normal Texture Path: " << materialDescriptionRead.normalTexturePath;
	//	std::cout << std::endl << std::endl;
	//}
	*/
}

void convertCameraFbxToLRC(FbxNode* pNode, std::string filename)
{
	fbxsdk::FbxCamera* camera = (fbxsdk::FbxCamera*)pNode->GetNodeAttribute();

	cameraStruct cameraCompacted;

	// Position, up-vector, forward-vector, FOV, near-distance, far-distance.

	cameraCompacted.Pos[0] = pNode->LclTranslation.Get().mData[0];
	cameraCompacted.Pos[1] = pNode->LclTranslation.Get().mData[1];
	cameraCompacted.Pos[2] = pNode->LclTranslation.Get().mData[2];

	cameraCompacted.UpVector[0] = camera->UpVector.Get().mData[0];
	cameraCompacted.UpVector[1] = camera->UpVector.Get().mData[1];
	cameraCompacted.UpVector[2] = camera->UpVector.Get().mData[2];

	cameraCompacted.FieldOfViewX = camera->FieldOfViewX.Get();
	cameraCompacted.FieldOfViewY = camera->FieldOfViewY.Get();

	cameraCompacted.NearDistance = camera->NearPlane.Get();
	cameraCompacted.FarDistance = camera->FarPlane.Get();

	FbxNode* pTargetNode = pNode->GetTarget();

	float intrestPosition[3];

	if (pTargetNode)
	{
		//DisplayString("        Camera Interest: ",(char *) pTargetNode->GetName());
		intrestPosition[0] = pTargetNode->LclTranslation.Get().mData[0];
		intrestPosition[1] = pTargetNode->LclTranslation.Get().mData[1];
		intrestPosition[2] = pTargetNode->LclTranslation.Get().mData[2];
	}
	else
	{
		//Display3DVector("        Default Camera Interest Position: ", camera->InterestPosition.Get());
		intrestPosition[0] = camera->InterestPosition.Get().mData[0];
		intrestPosition[1] = camera->InterestPosition.Get().mData[1];
		intrestPosition[2] = camera->InterestPosition.Get().mData[2];
	}

	// Normalize the intrestposition to get the cameras forward vector

	double mod = 0.0;
	for (size_t i = 0; i < 3; ++i)
	{
		mod += intrestPosition[i] * intrestPosition[i];
	}

	double mag = std::sqrt(mod);

	if (mag == 0)
	{
		throw std::logic_error("The input vector is a zero vector");
	}

	for (size_t i = 0; i < 3; ++i)
	{
		cameraCompacted.ForwardVector[i] = intrestPosition[i] / mag;
	}

	// ------------------------
	// ---- Write lrc file ----
	// ------------------------

	std::string lrcFilename;
	size_t indexBeforeFilename = filename.find_last_of("/") + 1;
	std::string endOfFilename = filename.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename) + "_" + pNode->GetName() + ".lrc";
	std::string destinationPath = "../Models/";
	lrcFilename = destinationPath + endOfFilename;

	std::cout << "Writing file..." << std::endl;

	std::ofstream myFile(lrcFilename, std::ios::out | std::ios::binary);

	myFile.write((char*)&cameraCompacted, sizeof(cameraStruct));

	myFile.close();

	std::cout << "File Written!" << std::endl << std::endl;
}

void convertLightFbxToLRM(FbxNode* pNode, std::string filename)
{
	fbxsdk::FbxLight* light = (fbxsdk::FbxLight*)pNode->GetNodeAttribute();

	lightStruct lightCompacted;

	lightCompacted.Pos[0] = pNode->LclTranslation.Get().mData[0];
	lightCompacted.Pos[1] = pNode->LclTranslation.Get().mData[1];
	lightCompacted.Pos[2] = pNode->LclTranslation.Get().mData[2];

	lightCompacted.Rotation[0] = pNode->LclRotation.Get().mData[0];
	lightCompacted.Rotation[1] = pNode->LclRotation.Get().mData[1];
	lightCompacted.Rotation[2] = pNode->LclRotation.Get().mData[2];

	lightCompacted.LightType = (LightType)light->LightType.Get();

	lightCompacted.CastLight = light->CastLight.Get();

	lightCompacted.DrawVolumetricLight = light->DrawVolumetricLight.Get();

	lightCompacted.DrawGroundProjection = light->DrawGroundProjection.Get();

	lightCompacted.DrawFrontFacingVolumetricLight = light->DrawFrontFacingVolumetricLight.Get();

	lightCompacted.Color[0] = light->Color.Get().mData[0];
	lightCompacted.Color[1] = light->Color.Get().mData[1];
	lightCompacted.Color[2] = light->Color.Get().mData[2];

	lightCompacted.Intensity = light->Intensity.Get();

	lightCompacted.InnerAngle = light->InnerAngle.Get();

	lightCompacted.OuterAngle = light->OuterAngle.Get();

	lightCompacted.Fog = light->Fog.Get();

	lightCompacted.DecayType = (LightDecayType)light->DecayType.Get();

	/*lightCompacted.FileName = */((std::string)light->FileName.Get()).copy(lightCompacted.FileName, 50);

	lightCompacted.EnableNearAttenuation = light->EnableNearAttenuation.Get();

	lightCompacted.NearAttenuationStart = light->NearAttenuationStart.Get();

	lightCompacted.NearAttenuationEnd = light->NearAttenuationEnd.Get();

	lightCompacted.EnableFarAttenuation = light->EnableFarAttenuation.Get();

	lightCompacted.FarAttenuationStart = light->FarAttenuationStart.Get();

	lightCompacted.FarAttenuationEnd = light->FarAttenuationEnd.Get();

	lightCompacted.CastShadows = light->CastShadows.Get();

	lightCompacted.ShadowColor[0] = light->ShadowColor.Get().mData[0];
	lightCompacted.ShadowColor[1] = light->ShadowColor.Get().mData[1];
	lightCompacted.ShadowColor[2] = light->ShadowColor.Get().mData[2];

	lightCompacted.AreaLightShape = (LightAreaLightShape)light->AreaLightShape.Get();

	lightCompacted.LeftBarnDoor = light->LeftBarnDoor.Get();

	lightCompacted.RightBarnDoor = light->RightBarnDoor.Get();

	lightCompacted.TopBarnDoor = light->TopBarnDoor.Get();

	lightCompacted.BottomBarnDoor = light->BottomBarnDoor.Get();

	lightCompacted.EnableBarnDoor = light->EnableBarnDoor.Get();

	// ------------------------
	// ---- Write lrl file ----
	// ------------------------

	std::string lrlFilename;
	size_t indexBeforeFilename = filename.find_last_of("/") + 1;
	std::string endOfFilename = filename.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename) + "_" + pNode->GetName() + ".lrl";
	std::string destinationPath = "../Models/";
	lrlFilename = destinationPath + endOfFilename;

	std::cout << "Writing file..." << std::endl;

	std::ofstream myFile(lrlFilename, std::ios::out | std::ios::binary);

	myFile.write((char*)&lightCompacted, sizeof(lightStruct));

	myFile.close();

	std::cout << "File Written!" << std::endl << std::endl;
}


bool copyFile(const char* source, const char* destination)
{
	std::ifstream src(source, std::ios::binary);
	std::ofstream dest(destination, std::ios::binary);
	dest << src.rdbuf();
	return src && dest;
}

inline void getAllJoints(Joint** finalJointsPtr, std::unordered_map<std::string, int>* boneIdxs, FbxNode* linkNode)
{
	FbxNodeAttribute::EType type = linkNode->GetNodeAttribute()->GetAttributeType();

	if (type != FbxNodeAttribute::EType::eSkeleton)
		return;

	Joint thisJoint;

	thisJoint.index = boneIdxs->at(linkNode->GetName());
	thisJoint.translation = linkNode->EvaluateLocalTransform().GetT();

	thisJoint.rotation = float4(linkNode->PreRotation.Get().mData[0], linkNode->PreRotation.Get().mData[1], linkNode->PreRotation.Get().mData[2], 0);

	//thisJoint.tempPoseRotation = linkNode->EvaluateLocalTransform().GetQ();
	// Debug prints
	/*
	std::cout << linkNode->GetName() << " idx: " << boneIdxs->at(linkNode->GetName()) << std::endl;
	std::cout << " PreRotation: " << linkNode->PreRotation.Get().mData[0] << ", " << linkNode->PreRotation.Get().mData[1] << ", " << linkNode->PreRotation.Get().mData[2] << ", " << std::endl;
	std::cout << " PostRotation: " << linkNode->PostRotation.Get().mData[0] << ", " << linkNode->PostRotation.Get().mData[1] << ", " << linkNode->PostRotation.Get().mData[2] << ", " << std::endl;
	std::cout << " LclRotation: " << linkNode->LclRotation.Get().mData[0] << ", " << linkNode->LclRotation.Get().mData[1] << ", " << linkNode->LclRotation.Get().mData[2] << ", " << std::endl;
	float3 lclRot = float3(linkNode->LclRotation.Get().mData);
	std::cout << lclRot.print(" LclRotation: ") << std::endl;;
	std::cout << float4(linkNode->EvaluateLocalTransform().GetR()).print(" EvaluateLocalTransform: ") << std::endl;;
	float3 LocalTransf = float3(linkNode->LclRotation.Get().mData);
	std::cout << LocalTransf.print(" EvaluateLocalTransform.GetR(): ") << std::endl;;
	std::cout << float4(linkNode->EvaluateGlobalTransform().GetR()).print(" EvaluateGlobalTransform: ") << std::endl;
	std::cout << float4(linkNode->GetGeometricRotation(fbxsdk::FbxNode::EPivotSet::eSourcePivot)).print(" GeometricRotation eSourcePivot: ") << std::endl;
	std::cout << float4(linkNode->GetGeometricRotation(fbxsdk::FbxNode::EPivotSet::eDestinationPivot)).print(" GeometricRotation eDestinationPivot: ") ;
	std::cout << float4(linkNode->EvaluateLocalTransform().GetQ()).print(" EvaluateLocalTransform Quaternion: ") << std::endl;
	std::cout << std::endl;
	*/

	thisJoint.nrOfChildren = linkNode->GetChildCount();

	if (thisJoint.nrOfChildren > MAX_CHILDREN_PER_JOINT)
		assert(false); // Right now we only support a certain number of children per joint, I'm sorry if it asserts here. 
					   // MAX_CHILDREN_PER_JOINT might need to be changed or the skeleton needs to be modified.

	for (int i = 0; i < thisJoint.nrOfChildren; i++)
	{
		FbxNode* child = linkNode->GetChild(i);
		std::string n = child->GetName();
		thisJoint.children[i] = boneIdxs->at(child->GetName());

		getAllJoints(finalJointsPtr, boneIdxs, child); // Recursive call
	}

	(*finalJointsPtr)[thisJoint.index] = thisJoint;
}

inline void getAllKeyFrames(std::vector<float>* keyFrameTimes, int& endFrame, std::unordered_map<std::string, int>* boneIdxs, FbxNode* linkNode, FbxAnimLayer* animLayer)
{
	FbxAnimCurve* animCurve = linkNode->LclTranslation.GetCurve(animLayer, FBXSDK_CURVENODE_COMPONENT_X);
	//animCurve->GetTimeInterval()
	if (!animCurve)
		return;

	int keyCount = animCurve->KeyGetCount();

	for (int key = 0; key < keyCount; key++)
	{
		float keyTime = (float)animCurve->KeyGetTime(key).GetSecondDouble();

		if (keyFrameTimes->size() == 0 || keyTime > keyFrameTimes->at(keyFrameTimes->size() - 1))
		{
			keyFrameTimes->push_back(keyTime);
			endFrame = animCurve->KeyGetTime(key).GetFrameCount();
			std::cout << (long)animCurve->KeyGetTime(key).GetFrameRate(animCurve->KeyGetTime(key).GetGlobalTimeMode()) << std::endl;
			continue;
		}

		bool found = false;
		for (int i = 0; i < keyFrameTimes->size(); i++)
		{
			if (keyTime == keyFrameTimes->at(i))
			{
				found = true;
				break;
			}

		}
		if (!found)
		{
			for (int i = 0; i < keyFrameTimes->size(); i++)
			{
				if (keyTime < keyFrameTimes->at(i))
				{
					keyFrameTimes->insert(keyFrameTimes->begin() + i, keyTime);
					break;
				}

			}
		}
		//keyFrameTimes->push_back((float)keyTime.GetSecondDouble());
	}


	for (int i = 0; i < linkNode->GetChildCount(); i++)
	{
		FbxNode* child = linkNode->GetChild(i);
		getAllKeyFrames(keyFrameTimes, endFrame, boneIdxs, child, animLayer); // Recursive call
	}

}

inline void getEndTime(double& endTime, FbxNode* linkNode, FbxAnimLayer* animLayer)
{
	if (linkNode->GetNodeAttribute()->GetAttributeType() != FbxNodeAttribute::EType::eSkeleton)
		return; // if the node isn't a joint

	FbxAnimCurve* animCurve = linkNode->LclTranslation.GetCurve(animLayer, FBXSDK_CURVENODE_COMPONENT_X);

	if (!animCurve)
		return;

	int keyCount = animCurve->KeyGetCount();

	if (keyCount > 0)
	{
		double timeOnLastKeyframe = animCurve->KeyGetTime(keyCount - 1).GetSecondDouble();

		if (timeOnLastKeyframe > endTime)
			endTime = timeOnLastKeyframe;
	}

	for (int i = 0; i < linkNode->GetChildCount(); i++)
	{
		FbxNode* child = linkNode->GetChild(i);
		getEndTime(endTime, child, animLayer); // Recursive call
	}
}
/*
inline void getAnimationFrames(Animation* currentAnimation, FbxNode* linkNode, double animationStopTime, double frameRate)
{
	FbxAnimEvaluator* eval = linkNode->GetAnimationEvaluator();

	int counter1 = 0;

	double frameRate = 30.0;
	for (double f = 0; f <= animationStopTime; f += (1.0/ frameRate))
	{
		fbxsdk::FbxTime frameTime;
		frameTime.SetSecondDouble(f);

		AnimationFrame currentFrame;

		// So i somehow forgot that each frame needs some kind of array with all the translations and rotations for all the joints
		// preferable with the same indices as the jointindices.

		currentFrame.timeStamp = f;

		JointTransformValues values;

		values.translation = eval->GetNodeLocalTransform(linkNode, frameTime).GetT();
		values.rotationQuat = eval->GetNodeLocalTransform(linkNode, frameTime).GetQ();

		currentFrame.jointTransforms.push_back(values);

		currentAnimation->frames.push_back(currentFrame);

		counter1++;
	}

	for (int c = 0; c < linkNode->GetChildCount(); c++)
	{
		FbxNode* child = linkNode->GetChild(c);

	}

	getAnimationFrames(Animation * currentAnimation, FbxNode * linkNode, double animationStopTime, double frameRate);
}
*/

inline void getAnimationFrames(AnimationFrame* currentFrame, std::unordered_map<std::string, int>* boneIdxs, FbxNode* linkNode, fbxsdk::FbxTime* frameTime, FbxAnimEvaluator* eval)
{
	if (linkNode->GetNodeAttribute()->GetAttributeType() != FbxNodeAttribute::EType::eSkeleton)
		return; // if the node isn't a joint

	JointTransformValues values;

	values.translation = eval->GetNodeLocalTransform(linkNode, *frameTime).GetT();
	values.rotationQuat = eval->GetNodeLocalTransform(linkNode, *frameTime).GetQ();

	int jointIndex = boneIdxs->at(linkNode->GetName());
	currentFrame->jointTransforms[jointIndex] = values;

	for (int c = 0; c < linkNode->GetChildCount(); c++)
	{
		FbxNode* child = linkNode->GetChild(c);
		getAnimationFrames(currentFrame, boneIdxs, child, frameTime, eval); // Recursive call
	}
}
