import React from 'react'
import { FaRegWindowClose } from 'react-icons/fa'
import { FaRegWindowMinimize } from 'react-icons/fa'

const TopBar: React.FC = () => {
  const handleMinimize = (): void => {
    window.electron.ipcRenderer.send('minimize')
  }
  const handleClose = (): void => {
    window.electron.ipcRenderer.send('close')
  }
  return (
    <div className="flex h-7 w-screen rounded-t-xl bg-black bg-opacity-60">
      <div
        id="top-bar"
        className="flex flex-auto items-center justify-center"
        style={{ WebkitAppRegion: 'drag' } as React.CSSProperties}
      >
        <div className="text-md text-white">Cron Monitor</div>
      </div>
      <div
        id="top-bar-buttons"
        className="absolute right-1 top-1 flex h-6"
        style={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}
      >
        <button
          type="button"
          onClick={handleMinimize}
          className="flex h-6 items-center justify-center p-1"
        >
          <FaRegWindowMinimize color="white" size="20" />
        </button>

        <button
          type="button"
          onClick={handleClose}
          className="flex h-6 items-center justify-center p-2"
        >
          <FaRegWindowClose color="white" size="20" />
        </button>
      </div>
    </div>
  )
}

export default TopBar
