import React, { useState } from 'react';
import { Paper, Box, Typography, Button, Alert } from '@mui/material';
import { Save, PlayArrow } from '@mui/icons-material';

const SAMPLE_SCENARIO = `{
  "scenario_id": "training_scenario_001",
  "title": "Basic Process Control",
  "description": "Introduction to process control operations",
  "author": "instructor",
  "seed": 12345,
  "initial_state": {
    "Streams.Feed.Temperature": 298.15,
    "Streams.Feed.Flow": 2.0
  },
  "events": [
    {
      "time_s": 60,
      "type": "set",
      "target": "Streams.Feed.Temperature",
      "payload": 310.15
    },
    {
      "time_s": 120,
      "type": "note",
      "payload": "Observe temperature change response"
    }
  ],
  "metadata": {
    "recommended_time_factor": 1.0,
    "expected_duration_seconds": 300,
    "difficulty": "beginner"
  }
}`;

const ScenarioEditor: React.FC = () => {
  const [scenarioJson, setScenarioJson] = useState(SAMPLE_SCENARIO);
  const [error, setError] = useState<string | null>(null);

  const handleSave = () => {
    try {
      JSON.parse(scenarioJson);
      setError(null);
      alert('Scenario saved successfully!');
    } catch (err: any) {
      setError(`Invalid JSON: ${err.message}`);
    }
  };

  const handleRun = () => {
    try {
      const scenario = JSON.parse(scenarioJson);
      console.log('Running scenario:', scenario.scenario_id);
      alert(`Scenario "${scenario.title}" would be executed here`);
    } catch (err: any) {
      setError(`Invalid JSON: ${err.message}`);
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">Scenario Editor</Typography>
        <Box>
          <Button startIcon={<Save />} variant="outlined" onClick={handleSave} sx={{ mr: 1 }}>
            Save
          </Button>
          <Button startIcon={<PlayArrow />} variant="contained" onClick={handleRun}>
            Run Scenario
          </Button>
        </Box>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Paper sx={{ p: 2 }}>
        <textarea
          value={scenarioJson}
          onChange={(e) => setScenarioJson(e.target.value)}
          style={{
            width: '100%',
            height: '500px',
            fontFamily: 'monospace',
            fontSize: '14px',
            backgroundColor: '#1e1e1e',
            color: '#d4d4d4',
            border: 'none',
            outline: 'none',
            padding: '16px',
            resize: 'vertical',
          }}
        />
      </Paper>

      <Box sx={{ mt: 2 }}>
        <Typography variant="body2" color="text.secondary">
          Edit the scenario JSON above. The schema supports event types: set, fault, controller, note, random_fault, alarm, operator_prompt.
        </Typography>
      </Box>
    </Box>
  );
};

export default ScenarioEditor;
