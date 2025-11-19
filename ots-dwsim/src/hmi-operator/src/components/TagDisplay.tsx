import { useState, useEffect } from 'react'
import { Box, Table, TableBody, TableCell, TableHead, TableRow, Typography, Chip } from '@mui/material'
import { ApiClient, TagValue } from '../services/ApiClient'

interface TagDisplayProps {
  sessionId: string
}

interface TagConfig {
  path: string
  label: string
}

const TAG_CONFIGS: TagConfig[] = [
  { path: 'Streams.Feed.Temperature', label: 'Feed Temperature' },
  { path: 'Streams.Feed.Pressure', label: 'Feed Pressure' },
  { path: 'Streams.Feed.MassFlow', label: 'Feed Flow' },
  { path: 'Streams.Product.Temperature', label: 'Product Temperature' },
]

export default function TagDisplay({ sessionId }: TagDisplayProps) {
  const [tags, setTags] = useState<Map<string, TagValue>>(new Map())

  useEffect(() => {
    const fetchTags = async () => {
      const newTags = new Map<string, TagValue>()

      for (const config of TAG_CONFIGS) {
        try {
          const value = await ApiClient.readTag(sessionId, config.path)
          if (value) {
            newTags.set(config.path, value)
          }
        } catch (error) {
          console.error(`Error reading tag ${config.path}:`, error)
        }
      }

      setTags(newTags)
    }

    // Initial fetch
    fetchTags()

    // Poll every second
    const interval = setInterval(fetchTags, 1000)

    return () => clearInterval(interval)
  }, [sessionId])

  const formatValue = (value: any): string => {
    if (typeof value === 'number') {
      return value.toFixed(2)
    }
    return String(value)
  }

  return (
    <Box>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Variable</TableCell>
            <TableCell align="right">Value</TableCell>
            <TableCell align="right">Units</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {TAG_CONFIGS.map((config) => {
            const tagValue = tags.get(config.path)
            return (
              <TableRow key={config.path}>
                <TableCell>{config.label}</TableCell>
                <TableCell align="right">
                  {tagValue ? (
                    <Typography variant="body2" sx={{ fontFamily: 'monospace', color: '#4CAF50' }}>
                      {formatValue(tagValue.value)}
                    </Typography>
                  ) : (
                    <Chip label="N/A" size="small" color="default" />
                  )}
                </TableCell>
                <TableCell align="right">
                  <Typography variant="caption" color="text.secondary">
                    {tagValue?.units || '-'}
                  </Typography>
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </Box>
  )
}
