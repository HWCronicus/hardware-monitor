import { contextBridge, ipcRenderer } from 'electron'
import { electronAPI } from '@electron-toolkit/preload'
import { HardwareMonitorData } from '../interfaces/hardware-monitor-data'

// Custom APIs for renderer
const api = {
  // Listen for hardware data updates from the WebSocket
  onHardwareData: (callback: (data: HardwareMonitorData) => void) => {
    ipcRenderer.on('hardware-data', (_event, data) => callback(data))
  },

  // Listen for WebSocket connection status updates
  onWebSocketStatus: (callback: (status: { connected: boolean; error?: string }) => void) => {
    ipcRenderer.on('websocket-status', (_event, status) => callback(status))
  },

  // Remove listeners when component unmounts
  removeHardwareDataListener: () => {
    ipcRenderer.removeAllListeners('hardware-data')
  },

  removeWebSocketStatusListener: () => {
    ipcRenderer.removeAllListeners('websocket-status')
  }
}

// Use `contextBridge` APIs to expose Electron APIs to
// renderer only if context isolation is enabled, otherwise
// just add to the DOM global.
if (process.contextIsolated) {
  try {
    contextBridge.exposeInMainWorld('electron', electronAPI)
    contextBridge.exposeInMainWorld('api', api)
  } catch (error) {
    console.error(error)
  }
} else {
  // @ts-ignore (define in dts)
  window.electron = electronAPI
  // @ts-ignore (define in dts)
  window.api = api
}
