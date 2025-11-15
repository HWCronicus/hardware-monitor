import React from 'react'

import GaugeComponent from 'react-gauge-component'

interface RadialGaugeProps {
  name: string
  value: number
  symbol: string
  bottomLabel?: string
}

const RadialGauge: React.FC<RadialGaugeProps> = ({ name, value, symbol, bottomLabel }) => {
  return (
    <div>
      <div className="text-auto text-center text-white">{name}</div>
      <div>
        <GaugeComponent
          arc={{
            gradient: false,
            colorArray: ['#00FF00', '#FFFA00', '#F58B19', '#FF0000']
          }}
          labels={{
            valueLabel: { formatTextValue: (value) => value + symbol },
            tickLabels: {
              type: 'outer',
              defaultTickValueConfig: {
                formatTextValue: (value: number) => value + symbol,
                style: { fontSize: 10 }
              },
              ticks: [{ value: 30 }, { value: 60 }, { value: 90 }]
            }
          }}
          value={value}
          minValue={0}
          maxValue={100}
          pointer={{ type: 'arrow', elastic: true }}
        />
      </div>
      <div className="text-auto text-center text-white">{bottomLabel}</div>
    </div>
  )
}

export default RadialGauge
// const UtlilizationGauge = (lable, usage): JSX.Element => (
//   <Gauge
//     value={usage}
// minValue={0}
// maxValue={100}
// renderTopLabel={`GPU ${lable}`}
// renderValue={({ value }) => `${value}%`}
// renderBottomLabel="km/h"
// arcColor={({ normValue }) => chroma.scale(['green', 'red'])(normValue).css()}
//   />
// )
