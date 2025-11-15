import { ElectronAPI } from '@electron-toolkit/preload'

interface HardwareMonitorAPI {
  onHardwareData: (callback: (data: unknown) => void) => void
  onWebSocketStatus: (callback: (status: { connected: boolean; error?: string }) => void) => void
  removeHardwareDataListener: () => void
  removeWebSocketStatusListener: () => void
}

declare global {
  interface Window {
    electron: ElectronAPI
    api: HardwareMonitorAPI
  }
}
