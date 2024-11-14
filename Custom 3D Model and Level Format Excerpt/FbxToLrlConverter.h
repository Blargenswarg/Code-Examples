#pragma once

void convertSceneFbxToLevel(FbxScene* pScene, std::string filename);
void convertContentFbxToLevel(FbxNode* pNode, std::string filename, std::vector<levelEntity>* entities, std::vector<levelPrefab>* prefabs);
void findComponentsInNode(FbxNode* pNode, levelEntity* entity, std::string filename);
void basicEntitySetup(FbxNode* pNode, levelEntity* entity, std::string name, std::vector<levelEntity>* entities);
void prefabSetup(FbxNode* pNode, std::vector<levelPrefab>* prefabs);
void meshCompSetup(FbxNode* pNode, levelEntity* entity, std::string filename, bool hasParent);

void saveParametersAndSize(FbxNode* pNode, std::vector<FbxProperty>* params, int& size);
void saveParamsToChar(FbxNode* componentNode, FbxNode* transformNode, std::vector<FbxProperty>* params, char* dataChar, int& offset);

// Loops through all the nodes in the loaded scene and calls convertContentFbxToLRM() with them
inline void convertSceneFbxToLevel(FbxScene* pScene, std::string filename)
{
	std::vector<levelEntity> entities;
	std::vector<levelPrefab> prefabs;
	
	FbxNode* lNode = pScene->GetRootNode();

	if (lNode)
	{
		for (int i = 0; i < lNode->GetChildCount(); i++)
		{
			convertContentFbxToLevel(lNode->GetChild(i), filename, &entities, &prefabs);
		}
	}

	// ------------------------------
	// ---- Write the level file ----
	// ------------------------------

	std::string FinalFilename;

	std::string destinationPath = "../../../DuplexEngine/StortSpel/res/levels/";

	size_t indexBeforeFilename = filename.find_last_of("/") + 1;
	std::string endOfFilename = filename.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename); 
	// endOfFilename is the name of the fbx but without .fbx and the path

	FinalFilename = destinationPath + endOfFilename + ".lrl";

	//std::cout << "destinationPath:	" << destinationPath << std::endl;
	//std::cout << "endOfFilename:	" << endOfFilename << std::endl << std::endl;

	std::ofstream myFile(FinalFilename, std::ios::out | std::ios::binary);

	int sizeOfPackage = 0;
	for (int i = 0; i < entities.size(); i++)
	{
		// Make one big char* package start with figuring out how big it should be.
		/*
		sizeOfPackage += sizeof(int);					// An int with the size of the name.
		sizeOfPackage += entities.at(i).name.length() + 2;  // The name
		//sizeOfPackage += sizeof(entities.at(i).name.c_str());  // The name
		sizeOfPackage += sizeof(float) * 10;			// All the floats of the entity transform
		sizeOfPackage += sizeof(bool);					// Phys: hasPhysics bool
		if (entities.at(i).hasPhysics)
		{
			sizeOfPackage += sizeof(bool);					// Phys: isDynamic bool
			sizeOfPackage += sizeof(GeometryType);			// Phys: type
			sizeOfPackage += sizeof(int);					// Phys: physMatName size int
			sizeOfPackage += entities.at(i).physMatName.length() + 2; // Phys: physMatName
		}
		sizeOfPackage += sizeof(int);					// An int with the number of components
		for (levelComponent& comp : entities.at(i).components)
		{
			sizeOfPackage += sizeof(int);				// An int with the size of the data
			sizeOfPackage += comp.sizeOfData;			// Size of the component data.
		}
		*/
		sizeOfPackage += entities.at(i).sizeOfEntity;
		for (levelComponent& comp : entities.at(i).components)
		{
			sizeOfPackage += sizeof(int);				// An int with the size of the data
			sizeOfPackage += comp.sizeOfData;			// Size of the component data.
		}
	}
	sizeOfPackage += sizeof(int);					// An int with the nr of prefabs
	for (levelPrefab& prefab : prefabs)
	{
		sizeOfPackage += sizeof(int);				// An int with the size of the data
		sizeOfPackage += prefab.sizeOfData;			// Size of the prefab data.
	}

	char* levelData = new char[sizeOfPackage];

	// Pack all the data into the levelData char*.
	int offset = 0;
	for (int i = 0; i < entities.size(); i++)
	{
		// The name
		writeStringToDataChar(levelData, entities.at(i).name, offset);

		// All the floats of the entity transform;
		writeDataToChar(levelData, entities.at(i).pos, offset);
		writeDataToChar(levelData, entities.at(i).rotQuat, offset);
		writeDataToChar(levelData, entities.at(i).scale, offset);

		// Physics stuff
		writeDataToChar(levelData, entities.at(i).hasPhysics, offset);
		if (entities.at(i).hasPhysics)
		{
			writeDataToChar(levelData, entities.at(i).isDynamic, offset);
			writeDataToChar(levelData, entities.at(i).geoType, offset);
			writeStringToDataChar(levelData, entities.at(i).physMatName, offset);
			writeDataToChar(levelData, entities.at(i).allMeshesHavePhys, offset);
		}

		// An int with the number of components
		int nrOfComps = entities.at(i).components.size();
		writeDataToChar(levelData,nrOfComps,offset);
					
		for (levelComponent& comp : entities.at(i).components)
		{
			// An int with the size of the data
			writeDataToChar(levelData, comp.sizeOfData, offset);
			
			// The data
			memcpy(levelData + offset, comp.data, comp.sizeOfData);
			offset += comp.sizeOfData;
		}
	}

	int nrOfPrefabs = prefabs.size();
	writeDataToChar(levelData, nrOfPrefabs, offset);					// An int with the nr of prefabs
	for (levelPrefab& prefab : prefabs)
	{
		writeDataToChar(levelData, prefab.sizeOfData, offset);	 // An int with the size of the data
		
		// The data
		memcpy(levelData + offset, prefab.data, prefab.sizeOfData);
		offset += prefab.sizeOfData;
	}

	// Write the amount of entities.
	int nrOfEntities = entities.size();
	myFile.write((char*)&nrOfEntities, sizeof(int));

	// Write how big the package is.
	myFile.write((char*)&sizeOfPackage, sizeof(int));

	// Write the package.
	myFile.write(levelData, sizeOfPackage);

	// Close file.
	myFile.close();
	
	assert(offset == sizeOfPackage);

	// Delete the levelData pointer and the components' char*
	delete[] levelData;
	for (int i = 0; i < entities.size(); i++)
	{
		for (levelComponent& comp : entities.at(i).components)
		{
			delete[] comp.data;
		}
	}
}

inline void convertContentFbxToLevel(FbxNode* pNode, std::string filename, std::vector<levelEntity>* entities, std::vector<levelPrefab>* prefabs)
{
	FbxNodeAttribute::EType lAttributeType;
	
	// I want to change it to:
	// Check for transforms that start with ent_ if we find a mesh we check if it has a parent that is an ent, (tho we shouldn't find that cause we'll only loop thro the children
	// that aren't ents) if the mesh is loose we make it into a simple meshcomponent entity, tho this won't be optimal we will allow it.
	// When we find an ent, we start a process that is proper

	if (pNode->GetNodeAttribute() == NULL)
	{
		FBXSDK_printf("NULL Node Attribute\n\n");
	}
	else
	{
		lAttributeType = (pNode->GetNodeAttribute()->GetAttributeType());

		//std::cout << "node name: " << pNode->GetName() << " node type:" << lAttributeType << std::endl;

		if (lAttributeType == FbxNodeAttribute::eNull)
		{
			std::string nodeName = pNode->GetName();
			
			bool isEntityRep = false;
			bool isPrefabRep = false;

			size_t indexAt_ = nodeName.find_first_of("_");

			if (indexAt_ != std::string::npos)
			{
				indexAt_++;
				std::string prefix = nodeName.substr(0, indexAt_);
				
				if (prefix == "ent_")
					isEntityRep = true;
				else if (prefix == "prefab_")
					isPrefabRep = true;
			}

			if (isEntityRep)
			{
				std::string suffix = nodeName.substr(indexAt_, nodeName.length());

				levelEntity entity;

				basicEntitySetup(pNode, &entity, suffix, entities);

				// find nodes representing components and add their data to the entity's component vector
				findComponentsInNode(pNode, &entity, filename);

				entities->push_back(entity);
			}
			else if (isPrefabRep)
			{
				prefabSetup(pNode, prefabs);
			}
			else
			{
				for (int i = 0; i < pNode->GetChildCount(); i++)
				{
					convertContentFbxToLevel(pNode->GetChild(i), filename, entities, prefabs);
				}
			}
		}
		else if (lAttributeType == FbxNodeAttribute::eMesh)
		{
			std::cout << "mesh: " << pNode->GetName() << " ---" << std::endl;

			// Save entity and MeshComponent
			levelEntity entity;

			basicEntitySetup(pNode, &entity, pNode->GetName(), entities);

			// Add meshcomponent
			meshCompSetup(pNode, &entity, filename, false);

			entities->push_back(entity);
		}
		else
		{
			for (int i = 0; i < pNode->GetChildCount(); i++)
			{
				convertContentFbxToLevel(pNode->GetChild(i), filename, entities, prefabs);
			}
		}
	}
}

inline void findComponentsInNode(FbxNode* pNode, levelEntity* entity, std::string filename)
{
	// Get the components and fill the levelEntity*'s vector
	for (int i = 0; i < pNode->GetChildCount(); i++)
	{
		FbxNode* child = pNode->GetChild(i);
		FbxNodeAttribute::EType lAttributeType = (child->GetNodeAttribute()->GetAttributeType());

		if (lAttributeType == FbxNodeAttribute::eNull)
		{
			bool isComponentRep = false;

			std::string compName = child->GetName();
			
			size_t indexAt_ = compName.find_first_of("_");

			if (indexAt_ != std::string::npos)
			{
				indexAt_++;
				std::string prefix = compName.substr(0, indexAt_);
				if (prefix == "comp_")
				{
					isComponentRep = true;
				}
			}

			if(isComponentRep)
			{
				compName = compName.substr(indexAt_, compName.length());
				
				levelComponent newComp;

				//int compNameSize = compName.length() + 2;
				
				//type enum
				FbxProperty compProp = child->FindProperty("compType", false);
				if (compProp.IsValid())
				{
					//std::string enumString = compProp.GetEnumValue(compProp.Get<int>()); //testing variable can be removed later
					newComp.type = (ComponentType)compProp.Get<int>();
				}
				else
					newComp.type = ComponentType::INVALID;

				// TODO: calculate the rest of the size of data
				newComp.sizeOfData = sizeof(ComponentType) + sizeof(int) + compName.length() + 2;
				
				std::vector<FbxProperty> params;

				saveParametersAndSize(child, &params, newComp.sizeOfData);

				newComp.data = new char[newComp.sizeOfData];

				int offset = 0;

				// Write comptype
				memcpy(newComp.data + offset, (int*)&newComp.type, sizeof(ComponentType));
				offset += sizeof(ComponentType);

				// Write name
				writeStringToDataChar(newComp.data, compName, offset);

				saveParamsToChar(child, pNode, &params, newComp.data, offset);

				assert(offset == newComp.sizeOfData);

				entity->components.push_back(newComp);
			}
			
			/*FbxProperty* p;
			p->GetPropertyDataType().GetType();
			FbxDataType::GetType();
			EFbxType::eFbxString;
			EFbxType::eFbxEnum;*/

		}
		else if (lAttributeType == FbxNodeAttribute::eMesh)
		{
			meshCompSetup(child, entity, filename, true);
		}
	}
}

inline void basicEntitySetup(FbxNode* pNode, levelEntity* entity, std::string name, std::vector<levelEntity>* entities)
{
	entity->name = name;
	
	entity->pos = pNode->EvaluateLocalTransform().GetT();
	entity->rotQuat = pNode->EvaluateLocalTransform().GetQ();
	entity->scale = pNode->EvaluateLocalTransform().GetS(); //FbxTime(), FbxNode::EPivotSet::eDestinationPivot
	entity->sizeOfEntity += sizeof(float) * 10;

	// Convert to lefthanded here.
	entity->pos.f[0] *= -1;
	entity->rotQuat.f[1] *= -1;
	entity->rotQuat.f[2] *= -1;

	std::string initialName = entity->name;
	int loops = 0;

	while (true)
	{
		int nrUsingName = 0;
		for (levelEntity& ent : *entities)
			if (ent.name == entity->name)
				nrUsingName++;

		if (nrUsingName > 0)
			entity->name = initialName + "-" + std::to_string(nrUsingName + loops);
		else
			break;

		loops++;
	}

	

	entity->sizeOfEntity += sizeOfStringInFile(entity->name);
	
	bool gotDefaultPhys = false;

	FbxProperty physParam = pNode->FindProperty(std::string("physicsParam1").c_str(), false);
	if (physParam.IsValid())
		entity->hasPhysics = physParam.Get<bool>();
	else
	{
		entity->hasPhysics = true;
		entity->isDynamic = false;
		entity->geoType = GeometryType::eBOX;
		entity->physMatName = "";
		entity->allMeshesHavePhys = false;

		gotDefaultPhys = true;
	}

	if (entity->hasPhysics && !gotDefaultPhys)
	{
		physParam = pNode->FindProperty(std::string("physicsParam2").c_str(), false);
		if (physParam.IsValid())
			entity->isDynamic = physParam.Get<bool>();
		else
			entity->isDynamic = false;

		physParam = pNode->FindProperty(std::string("physicsParam3").c_str(), false);
		if (physParam.IsValid())
			entity->geoType = (GeometryType)physParam.Get<int>();
		else
			entity->geoType = GeometryType::eBOX;

		physParam = pNode->FindProperty(std::string("physicsParam4").c_str(), false);
		if (physParam.IsValid())
		{
			entity->physMatName = physParam.Get<FbxString>().Buffer();
			if(entity->physMatName == "default")
				entity->physMatName = "";
		}	
		else
			entity->physMatName = "";

		physParam = pNode->FindProperty(std::string("physicsParam5").c_str(), false);
		if (physParam.IsValid())
			entity->allMeshesHavePhys = physParam.Get<bool>();
		else
			entity->allMeshesHavePhys = false;
	}

	entity->sizeOfEntity += sizeof(bool);				// Phys: hasPhysics bool
	if (entity->hasPhysics)
	{
		entity->sizeOfEntity += sizeof(bool);					// Phys: isDynamic bool
		entity->sizeOfEntity += sizeof(GeometryType);			// Phys: type
		entity->sizeOfEntity += sizeOfStringInFile(entity->physMatName);
		entity->sizeOfEntity += sizeof(bool);
	}
	
	entity->sizeOfEntity += sizeof(int); // An int with the number of components, all entities has this int.
}

inline void prefabSetup(FbxNode* pNode, std::vector<levelPrefab>* prefabs)
{
	levelPrefab newPrefab;
	
	// First calculate the size of the prefab's data
	
	newPrefab.sizeOfData = sizeof(float3) + sizeof(PrefabType); // Always has a float3 for pos and prefabtype.

	// Look for all params and get their size, (this could be a function, it happens twice)
	std::vector<FbxProperty> params;

	/*
	bool keepLooking = true;
	int paramIdx = 1;
	while (keepLooking)
	{
		FbxProperty compParam = pNode->FindProperty(std::string("param" + std::to_string(paramIdx)).c_str(), false);
		if (compParam.IsValid())
		{
			std::string tempStr;

			switch (compParam.GetPropertyDataType().GetType())
			{
			case fbxsdk::eFbxBool:		newPrefab.sizeOfData += sizeof(bool);   break;
			case fbxsdk::eFbxInt:		newPrefab.sizeOfData += sizeof(int);    break;
			case fbxsdk::eFbxFloat:		newPrefab.sizeOfData += sizeof(float);  break;
			case fbxsdk::eFbxDouble:	newPrefab.sizeOfData += sizeof(float);  break;
			case fbxsdk::eFbxDouble3:	newPrefab.sizeOfData += sizeof(float3); break;
			case fbxsdk::eFbxEnum:		newPrefab.sizeOfData += sizeof(int);    break;
			case fbxsdk::eFbxString:
				tempStr = compParam.Get<FbxString>().Buffer();
				newPrefab.sizeOfData += sizeOfStringInFile(tempStr);
				break;
			default:
				assert(false);
				break;
			}

			params.push_back(compParam);

			paramIdx++;
		}
		else
			keepLooking = false;
	}
	*/

	saveParametersAndSize(pNode, &params, newPrefab.sizeOfData);

	// Begin writing data to newPrefab

	newPrefab.data = new char[newPrefab.sizeOfData];

	int offset = 0;

	// Always write type
	/*
	FbxProperty prefabTypeParam = pNode->FindProperty("prefabType", false);
	if (prefabTypeParam.IsValid())
		newPrefab.type = (PrefabType)prefabTypeParam.Get<int>();
	else
		newPrefab.type = PrefabType::SCORE;
	writeDataToChar(newPrefab.data, newPrefab.type, offset);
	*/
	FbxProperty prefabTypeParam = pNode->FindProperty("prefabType", false);
	int type;
	if (prefabTypeParam.IsValid())
		type = prefabTypeParam.Get<int>();
	else
		type = (int)PrefabType::SCORE;
	writeDataToChar(newPrefab.data, type, offset);


	// Always write pos
	float3 pos;
	pos = pNode->EvaluateLocalTransform().GetT();
	pos.f[0] *= -1;
	writeDataToChar(newPrefab.data, pos, offset);

	saveParamsToChar(pNode, pNode, &params, newPrefab.data, offset);

	assert(offset == newPrefab.sizeOfData);

	prefabs->push_back(newPrefab);
}

inline void meshCompSetup(FbxNode* pNode, levelEntity* entity, std::string filename, bool hasParent)
{
	size_t indexBeforeFilename = filename.find_last_of("/") + 1;
	std::string meshName = filename.substr(indexBeforeFilename, filename.length() - 4 - indexBeforeFilename)
		+ std::string("_")
		+ pNode->GetName();

	bool isRigged = false;
	if (((FbxMesh*)pNode->GetNodeAttribute())->GetDeformerCount(FbxDeformer::eSkin) > 0)
		isRigged = true;
	
	std::string meshPath;

	bool noUniqueName = true;

	FbxProperty uniqueNameParam = pNode->FindProperty(std::string("uniqueName").c_str(), false);
	if (uniqueNameParam.IsValid())
	{
		meshPath = uniqueNameParam.Get<FbxString>().Buffer(); // If the mesh has an attrabute describing a unique name, we use that
		
		if (meshPath != "")
		{
			noUniqueName = false;
			meshPath.append(isRigged ? ".lrsm" : ".lrm");
		}
	}

	if(noUniqueName)
		meshPath = meshName + (isRigged ? ".lrsm" : ".lrm");

	bool visible = true;

	FbxProperty visibleParam = pNode->FindProperty(std::string("visible").c_str(), false);
	if (visibleParam.IsValid())
	{
		visible = visibleParam.Get<bool>();
	}

	//int compNameSize = meshName.length() + 2;
	//int compPathSize = meshPath.length() + 2;

	int materialCount = pNode->GetMaterialCount();
	std::string* matNames = new std::string[materialCount];
	std::string* matShaders = new std::string[materialCount];

	int matNamesSize = 0;

	if (materialCount > 0)
	{
		for (int mat = 0; mat < materialCount; mat++)
		{
			matNames[mat] = (std::string)pNode->GetMaterial(mat)->GetName();
			
			matNamesSize += sizeOfStringInFile(matNames[mat]);

			

			//pNode->GetMaterial(mat)->ShadingModel.
			//std::string sm = pNode->GetMaterial(mat)->ShadingModel.Get().Buffer(); //.Get<FbxString>().Buffer();
			//FbxProperty shaderParam = pNode->GetMaterial(mat)->FindProperty(std::string("ShaderProgram").c_str(), false);
			//if (shaderParam.IsValid())
			//{
			//	int aseazehy = 0;
			//}
		}
	}
	/*for (int i = 0; i < materialCount; i++)
	{
		std::string srhrh = matNames[i];
		int aseazehy = 0;
	}*/

	levelComponent meshComp;

	//					       comptype         +             name             +           path               +           (optional) transform
	meshComp.sizeOfData = sizeof(ComponentType) + sizeOfStringInFile(meshName) + sizeOfStringInFile(meshPath) + sizeof(bool) + (hasParent ? sizeof(float) * 10 : 0);
	meshComp.sizeOfData += sizeof(int) + matNamesSize; // Material count and names size.
	meshComp.sizeOfData += sizeof(bool); // Visibilty bool
	meshComp.type = (isRigged ? ComponentType::ANIM_MESH : ComponentType::MESH);

	// Fill the LevelComponent with data.

	meshComp.data = new char[meshComp.sizeOfData];

	int offset = 0;
	//memcpy(meshComp.data + offset, (int*)&meshComp.type, sizeof(ComponentType));
	//offset += sizeof(ComponentType);
	writeDataToChar(meshComp.data, meshComp.type,offset);

	writeStringToDataChar(meshComp.data, meshName, offset);
	writeStringToDataChar(meshComp.data, meshPath, offset);
	
	writeDataToChar(meshComp.data, materialCount, offset);
	for (int mat = 0; mat < materialCount; mat++)
	{
		writeStringToDataChar(meshComp.data, matNames[mat], offset);
	}

	//memcpy(meshComp.data + offset, &compNameSize, sizeof(int));
	//offset += sizeof(int);
	//memcpy(meshComp.data + offset, meshName.data(), compNameSize);
	//offset += compNameSize;
	//memcpy(meshComp.data + offset, meshPath.data(), compPathSize);							
	//offset += compPathSize;

	writeDataToChar(meshComp.data, hasParent, offset);
	if (hasParent)
	{
		float3 pos;
		float4 rotQuat;
		float3 scale;

		pos = pNode->EvaluateLocalTransform().GetT();
		rotQuat = pNode->EvaluateLocalTransform().GetQ();
		scale = pNode->EvaluateLocalTransform().GetS();

		pos.f[0] *= -1;
		rotQuat.f[1] *= -1;
		rotQuat.f[2] *= -1;

		writeDataToChar(meshComp.data, pos, offset);
		writeDataToChar(meshComp.data, rotQuat, offset);
		writeDataToChar(meshComp.data, scale, offset);
	}

	writeDataToChar(meshComp.data, visible, offset);
	
	entity->components.push_back(meshComp);

	delete[] matNames;
}

inline void saveParametersAndSize(FbxNode* pNode, std::vector<FbxProperty>* params, int& size)
{
	bool keepLooking = true;
	int paramIdx = 1;
	while (keepLooking)
	{
		FbxProperty compParam = pNode->FindProperty(std::string("param" + std::to_string(paramIdx)).c_str(), false);
		if (compParam.IsValid())
		{
			std::string tempStr;

			switch (compParam.GetPropertyDataType().GetType())
			{
			case fbxsdk::eFbxBool:		size += sizeof(bool);   break;
			case fbxsdk::eFbxShort:		size += sizeof(int);    break;
			case fbxsdk::eFbxInt:		size += sizeof(int);    break;
			case fbxsdk::eFbxFloat:		size += sizeof(float);  break;
			case fbxsdk::eFbxDouble:	size += sizeof(float);  break;
			case fbxsdk::eFbxDouble3:	size += sizeof(float3); break;
			case fbxsdk::eFbxEnum:		size += sizeof(int);    break;
			case fbxsdk::eFbxString:
				tempStr = compParam.Get<FbxString>().Buffer();

				// Special cases for when it's not a string but a vector representation
				if (tempStr == "pos" || tempStr == "scale" || tempStr == "entPos")
					size += sizeof(float3);
				else if (tempStr == "rot" || tempStr == "entRot")
					size += sizeof(float4);
				else
					size += sizeOfStringInFile(tempStr); // Else it is a string
				
				break;
			default:
				assert(false);
				break;
			}

			params->push_back(compParam);

			paramIdx++;
		}
		else
			keepLooking = false;
	}
}

inline void saveParamsToChar(FbxNode* componentNode, FbxNode* transformNode, std::vector<FbxProperty>* params, char* dataChar, int& offset)
{
	for (int par = 0; par < params->size(); par++)
	{
		
		switch (params->at(par).GetPropertyDataType().GetType())
		{
		case fbxsdk::eFbxBool:
		{
			bool boolParam = params->at(par).Get<bool>();
			writeDataToChar(dataChar, boolParam, offset);
			break;
		}
		case fbxsdk::eFbxShort:
		case fbxsdk::eFbxInt:
		{
			int intParam = params->at(par).Get<int>();
			writeDataToChar(dataChar, intParam, offset);
			break;
		}
		case fbxsdk::eFbxFloat:
		{
			float floatParam = params->at(par).Get<float>();
			writeDataToChar(dataChar, floatParam, offset);
			break;
		}
		case fbxsdk::eFbxDouble:
		{
			float floatParam = (float)params->at(par).Get<double>();
			writeDataToChar(dataChar, floatParam, offset);
			break;
		}
		case fbxsdk::eFbxDouble3:
		{
			FbxDouble3 float3Param1 = params->at(par).Get<FbxDouble3>();
			float3 float3Param;
			float3Param.f[0] = (float)float3Param1.mData[0];  float3Param.f[1] = (float)float3Param1.mData[1];  float3Param.f[2] = (float)float3Param1.mData[2];
			writeDataToChar(dataChar, float3Param, offset);
			break;
		}
		case fbxsdk::eFbxEnum:
		{
			int enumParam = params->at(par).Get<int>();
			writeDataToChar(dataChar, enumParam, offset);
			break;
		}
		case fbxsdk::eFbxString:
		{
			std::string tempStr = params->at(par).Get<FbxString>().Buffer();
			
			// Special cases for when it's not a string but a vector representation
			if (tempStr == "entPos")
			{
				float3 pos;
				pos = transformNode->EvaluateLocalTransform().GetT();
				pos.f[0] *= -1;
				writeDataToChar(dataChar, pos, offset);
			}
			else if (tempStr == "pos")
			{
				float3 pos;
				pos = componentNode->EvaluateLocalTransform().GetT();
				pos.f[0] *= -1;
				writeDataToChar(dataChar, pos, offset);
			}
			else if (tempStr == "entRot")
			{
				float4 rotQuat;
				rotQuat = transformNode->EvaluateLocalTransform().GetQ();
				rotQuat.f[1] *= -1;
				rotQuat.f[2] *= -1;
				writeDataToChar(dataChar, rotQuat, offset);
			}
			else if (tempStr == "rot")
			{
				float4 rotQuat;
				rotQuat = componentNode->EvaluateLocalTransform().GetQ();
				rotQuat.f[1] *= -1;
				rotQuat.f[2] *= -1;
				writeDataToChar(dataChar, rotQuat, offset);
			}
			else if (tempStr == "scale")
			{
				float3 scale;
				scale = transformNode->EvaluateLocalTransform().GetS();
				writeDataToChar(dataChar, scale, offset);
			}
			else // Else it is a string
				writeStringToDataChar(dataChar, tempStr, offset);
			break;
		}
		default:
			assert(false);
			break;
		}
	}
}

																														// What's left:
																														// -(done) A bool for kinematics in the reader (Ask oskar why it don't work)
																														// -(done) More init functions for components (ask edvin about rotAround. and tell him no tempstring)
																														// -(done) Support light components
																														// -(done) Remove nrOfParams
																														// -(done, could be better) integrate the material system the lads made.
																														// -(done, just warned for now) Mayyybe fix the hitboxes on offseted meshes, or maybe just warn about it.
																														// -(done) JAG GLÖMDE PREFABS!!!