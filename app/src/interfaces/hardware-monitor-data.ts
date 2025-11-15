export interface HardwareMonitorData {
  OperatingSystemInfo: OperatingSystemInfo
  devices: HardwareDevices
}

export interface OperatingSystemInfo {
  osName: string
  osVersion: string
  is64Bit: string
}

export interface HardwareDevices {
  cpu: CPU
  gpu: GPU[]
  motherboard: Motherboard
  ram: HardwareDevice
}

export interface HardwareDevice {
  name: string
  type: string
  description: string
  sensors: SensorData[]
}

export interface CPU {
  name: string
  type: string
  description: string
  frequency: SensorData[]
  power: SensorData[]
  temperature: SensorData[]
  voltage: SensorData[]
  load: SensorData[]
}

export interface GPU {
  name: string
  type: string
  description: string
  frequency: SensorData[]
  power: SensorData[]
  temperature: SensorData[]
  voltage: SensorData[]
  load: SensorData[]
  memory: SensorData[]
  fan: SensorData[]
}

export interface Motherboard {
  name: string
  type: string
  description: string
  power: SensorData[]
  temperature: SensorData[]
  voltage: SensorData[]
  fan: SensorData[]
}

export interface SensorData {
  name: string
  value: number
  unit: string
}
