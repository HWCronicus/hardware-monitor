import { HardwareMonitorData } from '@interfaces/hardware-monitor-data'
import { JSX, useEffect, useState } from 'react'
import RadialGauge from './RadialGauge'
import GaugeComponent from 'react-gauge-component'

interface WebSocketStatus {
  connected: boolean
  error?: string
}

export const HardwareMonitor = (): JSX.Element => {
  const [hardwareData, setHardwareData] = useState<HardwareMonitorData | null>(null)
  const [wsStatus, setWsStatus] = useState<WebSocketStatus>({ connected: false })

  useEffect(() => {
    window.api.onHardwareData((data: unknown) => {
      setHardwareData(data as HardwareMonitorData)
      console.log('Test', wsStatus.connected)
    })

    window.api.onWebSocketStatus((status: WebSocketStatus) => {
      setWsStatus({ connected: status.connected, error: status.error })
      console.log('WebSocket status:', status)
    })

    return () => {
      window.api.removeHardwareDataListener()
      window.api.removeWebSocketStatusListener()
    }
  }, [])

  const parseCpuUsage = (): number => {
    if (!hardwareData) return 0
    const cpuLoadSensor = hardwareData.devices.cpu.load.find(
      (sensor) => sensor.name === 'CPU Total'
    )
    return cpuLoadSensor ? cpuLoadSensor.value : 0
  }

  const parseGpuUsage = (): number => {
    if (!hardwareData) return 0
    const gpu = hardwareData.devices.gpu.find((gpu) => gpu.description === 'Dedicated GPU')
    if (!gpu) return 0
    const gpuLoadSensor = gpu.load.find((sensor) => sensor.name === 'GPU Core')
    return gpuLoadSensor ? gpuLoadSensor.value : 0
  }

  const parseRamUsage = (): number => {
    if (!hardwareData) return 0
    const ramUsageSensor = hardwareData.devices.ram.sensors.find(
      (sensor) => sensor.name === 'Memory'
    )
    return ramUsageSensor ? ramUsageSensor.value : 0
  }

  return (
    <div className="hardware-data">
      {hardwareData && wsStatus.connected ? (
        <div className="grid grid-cols-3 gap-4 p-4">
          <RadialGauge name="CPU Usage" value={parseCpuUsage()} symbol="%" bottomLabel="CPU" />
          <RadialGauge name="GPU Usage" value={parseGpuUsage()} symbol="%" bottomLabel="GPU" />
          <RadialGauge name="RAM Usage" value={parseRamUsage()} symbol="%" bottomLabel="RAM" />
        </div>
      ) : (
        <p className="text-center text-white">
          {wsStatus.connected ? 'Waiting for hardware data...' : 'Disconnected from server'}
        </p>
      )}
    </div>
  )
}
