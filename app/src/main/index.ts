import { app, shell, BrowserWindow, ipcMain, Task, Menu, Tray } from 'electron'
import { join } from 'path'
import { electronApp, optimizer, is } from '@electron-toolkit/utils'
import icon from '../../resources/icon.png?asset'
import WebSocket from 'ws'
import { exec } from 'child_process'
import { promisify } from 'util'

const execAsync = promisify(exec)

let mainWindow: BrowserWindow | null = null
let systemInfoWindow: BrowserWindow | null = null
let websocket: WebSocket | null = null
let isQuitting = false

const getServerPath = (): string => {
  if (is.dev) {
    // In development, use the exe from src/server
    return join(__dirname, '../../src/server/HardwareMonitor.exe')
  } else {
    // In production, use the exe from extraResources
    return join(process.resourcesPath, 'server/HardwareMonitor.exe')
  }
}

const startHardwareMonitorServer = async (): Promise<void> => {
  const serverPath = getServerPath()
  console.log('Starting hardware monitor server from:', serverPath)

  try {
    // Launch with admin privileges using PowerShell
    const psCommand = `Start-Process -FilePath "${serverPath}" -Verb RunAs -WindowStyle Hidden`

    await execAsync(`powershell -Command "${psCommand}"`, {
      windowsHide: true
    })

    console.log('Hardware monitor server started with admin privileges')
  } catch (error) {
    console.error('Failed to start hardware monitor server:', error)
    throw error
  }
}

const connectToHardwareMonitor = (): void => {
  const WEBSOCKET_URL = 'ws://localhost:42069'

  websocket = new WebSocket(WEBSOCKET_URL)

  websocket.on('open', () => {
    console.log('Connected to hardware monitor WebSocket server')
    if (mainWindow) {
      mainWindow.webContents.send('websocket-status', { connected: true })
    }
  })

  websocket.on('message', (data: WebSocket.Data) => {
    try {
      const hardwareData = JSON.parse(data.toString())
      if (mainWindow) {
        mainWindow.webContents.send('hardware-data', hardwareData)
      }
    } catch (error) {
      console.error('Error parsing hardware data:', error)
    }
  })

  websocket.on('error', (error) => {
    console.error('WebSocket error:', error)
    if (mainWindow) {
      mainWindow.webContents.send('websocket-status', { connected: false, error: error.message })
    }
  })

  websocket.on('close', () => {
    console.log('WebSocket connection closed. Attempting to reconnect in 5 seconds...')
    if (mainWindow) {
      mainWindow.webContents.send('websocket-status', { connected: false })
    }

    // Attempt to reconnect after 5 seconds (only if not quitting)
    if (!isQuitting) {
      setTimeout(() => {
        if (!isQuitting && (!websocket || websocket.readyState === WebSocket.CLOSED)) {
          connectToHardwareMonitor()
        }
      }, 5000)
    }
  })
}

const createWindow = (): void => {
  // Create the browser window.
  mainWindow = new BrowserWindow({
    width: 900,
    height: 670,
    show: false,
    frame: false,
    autoHideMenuBar: false,
    transparent: true,
    resizable: false,
    hasShadow: true,
    ...(process.platform === 'linux' ? { icon } : {}),
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      sandbox: false
    }
  })

  // Set up tray icon and menu
  const tasks: Task[] = [
    {
      program: process.execPath,
      arguments: '--show-window',
      iconPath: process.execPath,
      iconIndex: 0,
      title: 'Show Window',
      description: 'Bring up the minimized window'
    },
    {
      program: process.execPath,
      arguments: '--system-info',
      iconPath: process.execPath,
      iconIndex: 0,
      title: 'System Information',
      description: 'Bring up a new window of system information'
    },
    {
      program: process.execPath,
      arguments: '--quit',
      iconPath: process.execPath,
      iconIndex: 0,
      title: 'Quit',
      description: 'Quit the application'
    }
  ]

  app.setUserTasks(tasks)

  mainWindow.on('ready-to-show', () => {
    if (mainWindow) {
      mainWindow.show()
    }
  })

  mainWindow.webContents.setWindowOpenHandler((details) => {
    shell.openExternal(details.url)
    return { action: 'deny' }
  })

  // HMR for renderer base on electron-vite cli.
  // Load the remote URL for development or the local html file for production.
  if (is.dev && process.env['ELECTRON_RENDERER_URL']) {
    mainWindow.loadURL(process.env['ELECTRON_RENDERER_URL'])
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }

  if (process.argv.includes('--show-window')) {
    mainWindow.restore()
  }
  if (process.argv.includes('--quit')) {
    app.quit()
  }
  if (process.argv.includes('--system-info')) {
    createSystemInfoWindow()
  }
}

const createSystemInfoWindow = (): void => {
  // Create system info window if it doesn't exist
  if (!systemInfoWindow) {
    systemInfoWindow = new BrowserWindow({
      width: 800,
      height: 600,
      show: false,
      ...(process.platform === 'linux' ? { icon } : {}),
      webPreferences: {
        preload: join(__dirname, '../preload/index.js'),
        sandbox: false
      }
    })

    systemInfoWindow.on('closed', () => {
      systemInfoWindow = null
    })
  }

  // Load the info.html page
  if (is.dev && process.env['ELECTRON_RENDERER_URL']) {
    const developerUrl = process.env['ELECTRON_RENDERER_URL'].replace('index.html', 'info.html')
    systemInfoWindow.loadURL(developerUrl)
  } else {
    systemInfoWindow.loadFile(join(__dirname, '../renderer/info.html'))
  }

  // Show the window
  systemInfoWindow.show()
}

const setupTray = (): void => {
  const tray = new Tray(icon)

  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Show Window',
      click: (): void => {
        if (mainWindow) {
          mainWindow.show()
        }
      }
    },
    {
      label: 'System Information',
      click: (): void => {
        createSystemInfoWindow()
      }
    },
    {
      label: 'Quit',
      click: (): void => {
        app.quit()
      }
    }
  ])

  tray.setToolTip('Hardware Monitor')
  tray.setContextMenu(contextMenu)
}

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.whenReady().then(async () => {
  // Set app user model id for windows
  electronApp.setAppUserModelId('com.electron')

  // Default open or close DevTools by F12 in development
  // and ignore CommandOrControl + R in production.
  // see https://github.com/alex8088/electron-toolkit/tree/master/packages/utils
  app.on('browser-window-created', (_, window) => {
    optimizer.watchWindowShortcuts(window)
  })

  // IPC test
  ipcMain.on('ping', () => console.log('pong'))

  // IPC handlers for window controls
  ipcMain.on('minimize', () => {
    if (mainWindow) {
      mainWindow.minimize()
    }
  })

  ipcMain.on('close', () => {
    if (mainWindow) {
      mainWindow.close()
      app.quit()
    }
  })

  createWindow()

  // Setup system tray
  setupTray()

  // Start the C# hardware monitor server with admin privileges
  try {
    await startHardwareMonitorServer()

    // Wait a moment for the server to initialize
    setTimeout(() => {
      // Connect to the C# hardware monitor WebSocket server
      connectToHardwareMonitor()
    }, 2000)
  } catch {
    console.error('Failed to start hardware monitor server. Will attempt to connect anyway.')
    // Still try to connect in case the server is already running
    setTimeout(() => {
      connectToHardwareMonitor()
    }, 2000)
  }

  app.on('activate', function () {
    // On macOS it's common to re-create a window in the app when the
    // dock icon is clicked and there are no other windows open.
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

// Quit when all windows are closed, except on macOS. There, it's common
// for applications and their menu bar to stay active until the user quits
// explicitly with Cmd + Q.
app.on('window-all-closed', () => {
  isQuitting = true

  // Close WebSocket connection
  if (websocket) {
    websocket.close()
    websocket = null
  }

  if (process.platform !== 'darwin') {
    app.quit()
  }
})

// Cleanup when app is quitting
app.on('before-quit', (event) => {
  isQuitting = true

  // Close WebSocket
  if (websocket) {
    websocket.close()
    websocket = null
  }

  // On Windows, kill all HardwareMonitor.exe processes
  event.preventDefault()
  execAsync('taskkill /F /IM HardwareMonitor.exe', { windowsHide: true })
    .then(() => {
      console.log('Hardware monitor server stopped')
    })
    .catch(() => {
      // Process might not be running, ignore error
    })
    .finally(() => {
      app.exit(0)
    })
})
