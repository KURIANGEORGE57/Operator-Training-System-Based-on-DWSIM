import { useState, useEffect } from 'react'
import { Box, Typography } from '@mui/material'
import { ApiClient, TagValue } from '../services/ApiClient'

interface ProcessMimicProps {
  sessionId: string
}

interface TagData {
  [key: string]: TagValue | null
}

export default function ProcessMimic({ sessionId }: ProcessMimicProps) {
  const [tags, setTags] = useState<TagData>({})

  useEffect(() => {
    // Poll tags every second
    const interval = setInterval(async () => {
      try {
        // Example tags - customize based on your flowsheet
        const tagPaths = [
          'Streams.Feed.Temperature',
          'Streams.Feed.Pressure',
          'Streams.Product.Temperature',
        ]

        const tagData: TagData = {}
        for (const tagPath of tagPaths) {
          const value = await ApiClient.readTag(sessionId, tagPath)
          tagData[tagPath] = value
        }

        setTags(tagData)
      } catch (error) {
        console.error('Error reading tags:', error)
      }
    }, 1000)

    return () => clearInterval(interval)
  }, [sessionId])

  return (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
      {/* Simple SVG Process Flow Diagram */}
      <svg width="600" height="400" viewBox="0 0 600 400" style={{ border: '1px solid #444' }}>
        {/* Feed Stream */}
        <line x1="50" y1="200" x2="150" y2="200" stroke="#64B5F6" strokeWidth="4" />
        <text x="80" y="190" fill="#fff" fontSize="12">Feed</text>
        {tags['Streams.Feed.Temperature'] && (
          <text x="80" y="220" fill="#4CAF50" fontSize="10">
            {Number(tags['Streams.Feed.Temperature'].value).toFixed(1)} K
          </text>
        )}

        {/* Reactor */}
        <rect x="150" y="150" width="100" height="100" fill="#424242" stroke="#1976d2" strokeWidth="2" />
        <text x="170" y="205" fill="#fff" fontSize="14">Reactor</text>

        {/* Product Stream */}
        <line x1="250" y1="200" x2="350" y2="200" stroke="#FF9800" strokeWidth="4" />
        <text x="270" y="190" fill="#fff" fontSize="12">Product</text>
        {tags['Streams.Product.Temperature'] && (
          <text x="270" y="220" fill="#4CAF50" fontSize="10">
            {Number(tags['Streams.Product.Temperature'].value).toFixed(1)} K
          </text>
        )}

        {/* Outlet */}
        <line x1="350" y1="200" x2="450" y2="200" stroke="#66BB6A" strokeWidth="4" />
        <polygon points="450,200 435,195 435,205" fill="#66BB6A" />
      </svg>

      <Typography variant="caption" color="text.secondary" sx={{ mt: 2 }}>
        Simplified Process Flow Diagram - Customize based on your flowsheet
      </Typography>
    </Box>
  )
}
