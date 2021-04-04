#pragma once
#include "stdafx.h"

class Emulator;

class SaveStateManager
{
private:
	static constexpr uint32_t MaxIndex = 10;

	atomic<uint32_t> _lastIndex;
	shared_ptr<Emulator> _emu;

	string GetStateFilepath(int stateIndex);	
	void SaveScreenshotData(ostream& stream);
	bool GetScreenshotData(vector<uint8_t>& out, uint32_t& width, uint32_t& height, istream& stream);

public:
	static constexpr uint32_t FileFormatVersion = 8;

	SaveStateManager(shared_ptr<Emulator> emu);

	void SaveState();
	bool LoadState();

	void GetSaveStateHeader(ostream & stream);

	void SaveState(ostream &stream);
	bool SaveState(string filepath);
	void SaveState(int stateIndex, bool displayMessage = true);
	bool LoadState(istream &stream, bool hashCheckRequired = true);
	bool LoadState(string filepath, bool hashCheckRequired = true);
	bool LoadState(int stateIndex);

	void SaveRecentGame(string romName, string romPath, string patchPath);
	void LoadRecentGame(string filename, bool resetGame);

	int32_t GetSaveStatePreview(string saveStatePath, uint8_t* pngData);

	void SelectSaveSlot(int slotIndex);
	void MoveToNextSlot();
	void MoveToPreviousSlot();
};