#pragma once
#include "stdafx.h"
#include "IMemoryHandler.h"
#include "DebugTypes.h"

class RamHandler : public IMemoryHandler
{
private:
	uint8_t * _ram;
	uint32_t _offset;
	uint32_t _mask;

public:
	RamHandler(uint8_t *ram, uint32_t offset, uint32_t size, SnesMemoryType memoryType)
	{
		_ram = ram + offset;
		_offset = offset;
		_mask = (size - 1) & 0xFFF;
		_memoryType = memoryType;
	}

	uint8_t Read(uint32_t addr) override
	{
		return _ram[addr & _mask];
	}

	uint8_t Peek(uint32_t addr) override
	{
		return _ram[addr & _mask];
	}

	void Write(uint32_t addr, uint8_t value) override
	{
		_ram[addr & _mask] = value;
	}

	AddressInfo GetAbsoluteAddress(uint32_t address) override
	{
		AddressInfo info;
		info.Address = _offset + (address & _mask);
		info.Type = _memoryType;
		return info;
	}
};