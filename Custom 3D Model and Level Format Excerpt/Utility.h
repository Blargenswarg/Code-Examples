#pragma once
#include <iostream>
#include <vector>
#include <unordered_map>
#include <fstream>
#include <string>
#include <assert.h>
#include <filesystem>
#include "fbxStuff.h"

template <typename T>
inline void writeDataToChar(char* theChar, T& data, int& offset)
{
	memcpy(theChar + offset, &data, sizeof(T));
	offset += sizeof(T);
}

template <typename T>
inline void writeDataToChar(char* theChar, T& data, int& offset, int& size)
{
	memcpy(theChar + offset, &data, size);
	offset += size;
}

inline void writeStringToDataChar(char* data, std::string& str, int& offset)
{
	int nameSize = str.length() + 2;

	memcpy(data + offset, &nameSize, sizeof(int));
	offset += sizeof(int);

	if (nameSize == 2)
		return;

	char* entName = str.data();
	memcpy(data + offset, entName, nameSize);
	offset += nameSize;
}
/*
inline int sizeOfStringInFile(std::string& str)
{
	return sizeof(int) + ((str.length() == 0) ? 0 : (str.length() + 2));
}
*/

#define sizeOfStringInFile(str) sizeof(int) + ((str.length() == 0) ? 0 : (str.length() + 2))

struct float4
{
	float f[4];
	float4() {}
	float4(const float& a, const float& b, const float& c, const float& d) { f[0] = a; f[1] = b; f[2] = c; f[3] = d; }
	float4(const float a[4]) { for (int i = 0; i < 4; i++) { f[i] = a[i]; } }
	float4(const double a[4]) { for (int i = 0; i < 4; i++) { f[i] = (float)a[i]; } }
	bool operator==(const float4& a) const { return (f[0] == a.f[0] && f[1] == a.f[1] && f[2] == a.f[2] && f[3] == a.f[3]); }
	float4 operator+(const float4& a) const { return float4{ f[0] + a.f[0], f[1] + a.f[1], f[2] + a.f[2], f[3] + a.f[3] }; }
	float4 operator*(const float4& a) const { return float4{ f[0] * a.f[0], f[1] * a.f[1], f[2] * a.f[2], f[3] * a.f[3] }; }
	float4 operator*(const float& a) const { return float4{ f[0] * a, f[1] * a, f[2] * a, f[3] * a }; }
	void operator=(const float4& a) { for (int i = 0; i < 4; i++) { f[i] = a.f[i]; } }
	void operator=(const float a[4]) { for (int i = 0; i < 4; i++) { f[i] = a[i]; } }
	void operator=(const double a[4]) { for (int i = 0; i < 4; i++) { f[i] = (float)a[i]; } }
	void operator=(const fbxsdk::FbxVector4& a) { f[0] = (float)a[0];  f[1] = (float)a[1];  f[2] = (float)a[2]; f[3] = (float)a[3]; }
	std::string print() { return std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]) + ", " + std::to_string(f[3]); }
	std::string print(std::string start) { return start + std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]) + ", " + std::to_string(f[3]); }
};
struct float3
{
	float f[3];
	float3() {}
	float3(const float& a, const float& b, const float& c) { f[0] = a; f[1] = b; f[2] = c; }
	float3(const float a[3]) { for (int i = 0; i < 3; i++) { f[i] = a[i]; } }
	float3(const double a[3]) { for (int i = 0; i < 3; i++) { f[i] = (float)a[i]; } }
	bool operator==(const float3 & a) const { return (f[0] == a.f[0] && f[1] == a.f[1] && f[2] == a.f[2]); }
	float3 operator+(const float3& a) const { return float3{ f[0] + a.f[0], f[1] + a.f[1], f[2] + a.f[2] }; }
	float3 operator-(const float3& a) const { return float3{ f[0] - a.f[0], f[1] - a.f[1], f[2] - a.f[2] }; }
	float3 operator*(const float3 & a) const { return float3{ f[0] * a.f[0], f[1] * a.f[1], f[2] * a.f[2] }; }
	float3 operator*(const float & a) const { return float3{ f[0] * a, f[1] * a, f[2] * a }; }
	void operator=(const float3& a) { for (int i = 0; i < 3; i++) { f[i] = a.f[i]; } }
	void operator=(const float a[3]) { for (int i = 0; i < 3; i++) { f[i] = a[i]; } }
	void operator=(const double a[3]) { for (int i = 0; i < 3; i++) { f[i] = (float)a[i]; } }
	void operator=(const fbxsdk::FbxVector4 & a) { f[0] = (float)a[0];  f[1] = (float)a[1];  f[2] = (float)a[2]; }
	void operator=(const fbxsdk::FbxDouble4 & a) { f[0] = (float)a.mData[0];  f[1] = (float)a.mData[1];  f[2] = (float)a.mData[2]; }
	void operator=(const fbxsdk::FbxDouble3 & a) { f[0] = (float)a.mData[0];  f[1] = (float)a.mData[1];  f[2] = (float)a.mData[2]; }
	std::string print() { return std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]); }
	std::string print(std::string start) { return start + std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]); }
	void normalize()
	{
		double mod = 0.0;
		for (int i = 0; i < 3; i++)
			mod += f[i] * f[i];

		double mag_inv = 1.0 / std::sqrt(mod);
		
		for (int i = 0; i < 3; i++)
			f[i] *= mag_inv;
	}

	static float dot(float3 a, float3 b)
	{
		return a.f[0] * b.f[0] + a.f[1] * b.f[1] + a.f[2] * b.f[2];
	}

	static float3 cross(float3 a, float3 b)
	{
		return float3(a.f[1] * b.f[2] - a.f[2] * b.f[1], a.f[0] * b.f[2] - a.f[2] * b.f[0], a.f[0] * b.f[1] - a.f[1] * b.f[0]);
		
		//return Vector3(y * a.z - z * a.y, x * a.z - z * a.x, x * a.y - y * a.x);
	}
};
struct float2
{
	float f[2];
	float2() {}
	float2(const float& a, const float& b) { f[0] = a; f[1] = b; }
	float2(const float a[2]) { for (int i = 0; i < 2; i++) { f[i] = a[i]; } }
	float2(const double a[2]) { for (int i = 0; i < 2; i++) { f[i] = (float)a[i]; } }
	void operator=(const float2& a) { for (int i = 0; i < 2; i++) { f[i] = a.f[i]; } }
	void operator=(const float a[2]) { for (int i = 0; i < 2; i++) { f[i] = a[i]; } }
	void operator=(const double a[2]) { for (int i = 0; i < 2; i++) { f[i] = (float)a[i]; } }
	void operator=(const fbxsdk::FbxVector2 & a) { f[0] = (float)a[0];  f[1] = (float)a[1]; }
	std::string print() { return std::to_string(f[0]) + ", " + std::to_string(f[1]); }
	std::string print(std::string start) { return start + std::to_string(f[0]) + ", " + std::to_string(f[1]); }
};

struct int4
{
	int f[4];
	int4() {}
	int4(const int& a, const int& b, const int& c, const int& d) { f[0] = a; f[1] = b; f[2] = c; f[3] = d; }
	int4(const int a[4]) { for (int i = 0; i < 4; i++) { f[i] = a[i]; } }
	void operator=(const int4& a) { for (int i = 0; i < 4; i++) { f[i] = a.f[i]; } }
	void operator=(const int a[4]) { for (int i = 0; i < 4; i++) { f[i] = a[i]; } }
	bool operator==(const int4& a) const { return (f[0] == a.f[0] && f[1] == a.f[1] && f[2] == a.f[2] && f[3] == a.f[3]); }
	std::string print() { return std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]) + ", " + std::to_string(f[3]); }
	std::string print(std::string start) { return start + std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]) + ", " + std::to_string(f[3]); }
};
struct int3
{
	int f[3];
	int3() {}
	int3(const int& a, const int& b, const int& c) { f[0] = a; f[1] = b; f[2] = c; }
	int3(const int a[3]) { for (int i = 0; i < 3; i++) { f[i] = a[i]; } }
	void operator=(const int3& a) { for (int i = 0; i < 3; i++) { f[i] = a.f[i]; } }
	void operator=(const int a[3]) { for (int i = 0; i < 3; i++) { f[i] = a[i]; } }
	bool operator==(const int3 & a) const { return (f[0] == a.f[0] && f[1] == a.f[1] && f[2] == a.f[2]); }
	std::string print() { return std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]); }
	std::string print(std::string start) { return start + std::to_string(f[0]) + ", " + std::to_string(f[1]) + ", " + std::to_string(f[2]); }
};
struct int2
{
	int f[2];
	int2() {}
	int2(const int& a, const int& b) { f[0] = a; f[1] = b; }
	int2(const int a[2]) { for (int i = 0; i < 2; i++) { f[i] = a[i]; } }
	void operator=(const int2& a) { for (int i = 0; i < 2; i++) { f[i] = a.f[i]; } }
	void operator=(const int a[2]) { for (int i = 0; i < 2; i++) { f[i] = a[i]; } }
	bool operator==(const int2 & a) const { return (f[0] == a.f[0] && f[1] == a.f[1]); }
};

struct LRM_VERTEX
{
	float3 pos;
	float2 texCoord;
	float3 normal;
	float3 tangent;
	float3 bitangent;
};    // a struct to define a vertex

struct LRSM_VERTEX
{
	float3 pos;
	float2 texCoord;
	float3 normal;
	float3 tangent;
	float3 bitangent;
	int4   boneIdxs;
	float4 weights;
};    // a struct to define a vertex

struct ABM_VERTEX
{
	float3 pos;
	float2 texCoord;
	float3 normal;
	float color[4];
};    // a struct to define a vertex

struct comboIdxList
{
	int nrOfcomboIdxs = 0;
	std::vector<int> comboIdx;
};

struct jointConnections
{
	int finalJoints[4] = {-1, -1, -1, -1};
	double finalWeights[4] = { -1, -1, -1, -1 };

	std::vector<int> allJoints;
	std::vector<double> allWeights;
};

#define MAX_CHILDREN_PER_JOINT 5

struct Joint
{
	int index;
	int nrOfChildren;
	int children[MAX_CHILDREN_PER_JOINT];
	
	float3 translation;
	float4 rotation;

	Joint() { for (int i = 0; i < MAX_CHILDREN_PER_JOINT; i++) { children[i] = -1; } }
};

struct JointTransformValues
{
	float3 translation;
	float4 rotationQuat;
};

struct AnimationFrame
{
	float timeStamp;
	//std::vector<JointTransformValues> jointTransforms;
	JointTransformValues* jointTransforms;
};

struct Animation
{
	float timeSpan;
	std::vector<AnimationFrame> frames;
};

enum ComponentType
{
	MESH,
	ANIM_MESH,
	AUDIO, 
	PHYSICS, 
	CHARACTERCONTROLLER, 
	TRIGGER, 
	TEST, 
	INVALID, 
	UNASSIGNED, 
	ROTATEAROUND, 
	ROTATE, 
	LIGHT, 
	SWEEPING, 
	FLIPPING, 
	CHECKPOINT,
	TRAP, 
	PARTICLE, 
	PROJECTILE,
	GROW, 
	SHRINK, 
	SWING, 
	SWEEPING2
};

enum GeometryType
{
	eSPHERE,
	ePLANE,
	eCAPSULE,
	eBOX,
	eCONVEXMESH,
	eTRIANGLEMESH,
};

struct levelComponent
{
	ComponentType type;
	int sizeOfData;
	char* data;
};

struct levelEntity
{
	std::vector<levelComponent> components;
	std::string name;
	float3 pos;
	float4 rotQuat;
	float3 scale;

	int sizeOfEntity = 0;

	// Physics stuff
	bool hasPhysics;
	bool isDynamic;
	GeometryType geoType;
	std::string physMatName;
	bool allMeshesHavePhys;
};

enum PrefabType
{
	PARIS_WHEEL,
	FLIPPING_PLATFORM,
	SWEEPING_PLATFORM,
	PICKUP,
	SCORE,
	pfCHECKPOINT,
	SLOWTRAP,
	PUSHTRAP,
	BARRELDROP,
	GOAL_TRIGGER
};

struct levelPrefab
{
	PrefabType type;
	int sizeOfData;
	char* data;
};

struct cameraStruct
{
	// Position, up-vector, forward-vector, FOV, near-distance, far-distance.
	double Pos[3];
	double UpVector[3];
	double ForwardVector[3];
	double FieldOfViewX;
	double FieldOfViewY;
	double NearDistance;
	double FarDistance;
};

/* FbxLight enums */
enum LightType
{
	ePoint,
	eDirectional,
	eSpot,
	eArea,
	eVolume
};

enum LightDecayType
{
	eNone,
	eLinear,
	eQuadratic,
	eCubic
};

enum LightAreaLightShape
{
	eRectangle,
	eSphere
};

struct lightStruct
{
	double Pos[3];
	double Rotation[3];
	
	LightType LightType;
	bool CastLight;
	bool DrawVolumetricLight;
	bool DrawGroundProjection;
	bool DrawFrontFacingVolumetricLight;
	double Color[3];
	double Intensity;
	double InnerAngle;
	double OuterAngle;
	double Fog;
	LightDecayType DecayType;
	double DecayStart;
	char FileName[50];
	bool EnableNearAttenuation;
	double NearAttenuationStart;
	double NearAttenuationEnd;
	bool EnableFarAttenuation;
	double FarAttenuationStart;
	double FarAttenuationEnd;
	bool CastShadows;
	double ShadowColor[3];
	LightAreaLightShape AreaLightShape;
	float LeftBarnDoor;
	float RightBarnDoor;
	float TopBarnDoor;
	float BottomBarnDoor;
	bool EnableBarnDoor;
};

struct MaterialDescription
{
	char materialName[50] = { 0 };
	float ambient[3];
	float diffuse[3];
	float emissive[3];
	float opacity;
	char diffuseTextureName[50] = { 0 };
	char diffuseTexturePath[50] = { 0 };
	char normalTextureName[50] = { 0 };
	char normalTexturePath[50] = { 0 };
};

/* 
//  component list for maya, i guess
	MESH:
	ANIM_MESH:
	AUDIO:
	PHYSICS:
	CHARACTERCONTROLLER:
	TRIGGER:
	TEST:
	INVALID:
	UNASSIGNED:
	ROTATEAROUND:
	ROTATE:
	LIGHT:
	SWEEPING:
	FLIPPING:
	CHECKPOINT:
	TRAP:
	PARTICLE:
	PROJECTILE

MESH:ANIM_MESH:AUDIO:PHYSICS:CHARACTERCONTROLLER:TRIGGER:TEST:INVALID:UNASSIGNED:ROTATEAROUND:ROTATE:LIGHT:SWEEPING:FLIPPING:CHECKPOINT:TRAP:PARTICLE:PROJECTILE
*/
/*
eSPHERE:
ePLANE:
eCAPSULE:
eBOX:
eCONVEXMESH:
eTRIANGLEMESH:

eSPHERE:ePLANE:eCAPSULE:eBOX:eCONVEXMESH:eTRIANGLEMESH
*/
/*
PARIS_WHEEL:
FLIPPING_PLATFORM:
SWEEPING_PLATFORM:
PICKUP:
SCORE:
CHECKPOINT:
SLOWTRAP:
PUSHTRAP:
BARRELDROP:
GOAL_TRIGGER:

PARIS_WHEEL:FLIPPING_PLATFORM:SWEEPING_PLATFORM:PICKUP:SCORE:CHECKPOINT:SLOWTRAP:PUSHTRAP:BARRELDROP:GOAL_TRIGGER:
*/

/*
// switch-case for testing fbxproperties

switch (compParam.GetPropertyDataType().GetType())
{
case fbxsdk::eFbxShort:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxUShort:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxUInt:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxLongLong:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxULongLong:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxHalfFloat:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxBool:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxInt:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxFloat:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxDouble:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxDouble2:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxDouble3:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxDouble4:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxDouble4x4:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxEnum:
	aehaehkeysmash = 1;
	break;
case fbxsdk::eFbxString:
	aehaehkeysmash = 1;
	break;
default:
	assert(false);
	break;
}

def createNewComp():
	currentRow = ui.componentDropdown.currentIndex()

	if compTypes[currentRow] in supportedComponents:
		print "Adding: %s"%(compTypes[currentRow])
		newComp = pm.createNode('transform', n = "comp_%s"%(compTypes[currentRow]))
		pm.parent(newComp, ui.entLabel.text())
		pm.select(ui.entLabel.text())
		addCompType(newComp, currentRow)

	if compTypes[currentRow] == "MESH":
		print "no"
	elif compTypes[currentRow] == "ANIM_MESH": # TODO: make the button unclickable when a forbidden comp is input
		print "no"
	elif compTypes[currentRow] == "AUDIO":
		createStringParam( newComp, 1, "Audio File", "fireplace.wav" )
		createParam( newComp, 2, "Loop", "bool", 1 )
		createParam( newComp, 3, "Volume", "float", 0.5 )
		createParam( newComp, 4, "Pitch", "float", 0.5 )
		createParam( newComp, 5, "Is Positional", "bool", 1 )
	elif compTypes[currentRow] == "PHYSICS":
		print "no"
	elif compTypes[currentRow] == "TRIGGER":
		print "no"
	elif compTypes[currentRow] == "TEST":
		print "no"
	elif compTypes[currentRow] == "INVALID":
		print "no"
	elif compTypes[currentRow] == "UNASSIGNED":
		print "no"
	elif compTypes[currentRow] == "ROTATEAROUND":
		print "no"
	elif compTypes[currentRow] == "ROTATE":
		createVectorParam(newComp, 1, "Axis", [0,1,0])
		createParam( newComp, 2, "Speed", "float", 5 )

	elif compTypes[currentRow] == "LIGHT":
		createEnumParam( newComp, 1, "Light Type", "POINT:SPOT", 0 )
		createStringParam( newComp, 2, "pos (don't edit)", "pos" )
		createVectorParam(newComp, 3, "Color", [1,1,1])
		createParam( newComp, 4, "intensity", "float", 1 )
		createStringParam( newComp, 5, "rot (don't edit)", "rot" )

	elif compTypes[currentRow] == "SWEEPING":
		createStringParam( newComp, 1, "pos (don't edit)", "entPos" )
		createVectorParam(newComp, 2, "End Position", [0,0,0])
		createParam( newComp, 3, "Travel Time", "float", 5 )
		createParam( newComp, 4, "Single Sweeps", "bool", 0 )

	elif compTypes[currentRow] == "FLIPPING":
		createParam( newComp, 1, "Up Time", "float", 1 )
		createParam( newComp, 2, "Down Time", "float", 1 )
		createParam( newComp, 3, "Flip Speed", "float", 5 )
	elif compTypes[currentRow] == "CHECKPOINT":
		print "no"
	elif compTypes[currentRow] == "TRAP":
		print "no"
	elif compTypes[currentRow] == "PARTICLE":
		print "no"
	elif compTypes[currentRow] == "PROJECTILE":
		print "no"
	else:
		print "nope"

*/