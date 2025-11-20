import React, { useEffect, useState } from 'react';
import {
  Paper,
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
  Card,
  CardContent,
  Grid,
  LinearProgress,
  IconButton,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Alert,
} from '@mui/material';
import {
  Assessment,
  ExpandMore,
  Refresh,
  CheckCircle,
  Cancel,
  Timeline,
} from '@mui/icons-material';
import ApiClient from '../services/ApiClient';
import { Session, AssessmentReport, PerformanceMetrics } from '../types';

const AssessmentViewer: React.FC = () => {
  const [sessions, setSessions] = useState<Session[]>([]);
  const [assessments, setAssessments] = useState<AssessmentReport[]>([]);
  const [selectedAssessment, setSelectedAssessment] = useState<AssessmentReport | null>(null);
  const [selectedSessionId, setSelectedSessionId] = useState<string>('');
  const [generateDialogOpen, setGenerateDialogOpen] = useState(false);
  const [metricsDialogOpen, setMetricsDialogOpen] = useState(false);
  const [selectedMetrics, setSelectedMetrics] = useState<PerformanceMetrics | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadSessions = async () => {
    try {
      const data = await ApiClient.listSessions();
      setSessions(data);
    } catch (error) {
      console.error('Failed to load sessions:', error);
    }
  };

  const loadAssessments = async () => {
    try {
      setLoading(true);
      const filter = selectedSessionId || undefined;
      const data = await ApiClient.listAssessments(filter);
      setAssessments(data);
      setError(null);
    } catch (error: any) {
      console.error('Failed to load assessments:', error);
      setError('Failed to load assessments: ' + error.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSessions();
    loadAssessments();
  }, [selectedSessionId]);

  const handleGenerateAssessment = async () => {
    if (!selectedSessionId) return;

    try {
      setLoading(true);
      setError(null);
      const report = await ApiClient.generateAssessment(selectedSessionId);
      setGenerateDialogOpen(false);
      setSelectedAssessment(report);
      await loadAssessments();
    } catch (error: any) {
      console.error('Failed to generate assessment:', error);
      setError('Failed to generate assessment: ' + error.message);
    } finally {
      setLoading(false);
    }
  };

  const handleViewMetrics = async (sessionId: string) => {
    try {
      setLoading(true);
      const metrics = await ApiClient.getSessionMetrics(sessionId);
      setSelectedMetrics(metrics);
      setMetricsDialogOpen(true);
    } catch (error: any) {
      console.error('Failed to load metrics:', error);
      setError('Failed to load metrics: ' + error.message);
    } finally {
      setLoading(false);
    }
  };

  const getGradeColor = (grade: string) => {
    switch (grade) {
      case 'A': return 'success';
      case 'B': return 'info';
      case 'C': return 'warning';
      case 'D': return 'warning';
      case 'F': return 'error';
      default: return 'default';
    }
  };

  const formatDuration = (isoDuration: string): string => {
    try {
      // Parse ISO 8601 duration (e.g., "PT2H30M45S")
      const match = isoDuration.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?/);
      if (!match) return isoDuration;

      const hours = parseInt(match[1] || '0');
      const minutes = parseInt(match[2] || '0');
      const seconds = parseFloat(match[3] || '0');

      const parts = [];
      if (hours > 0) parts.push(`${hours}h`);
      if (minutes > 0) parts.push(`${minutes}m`);
      if (seconds > 0) parts.push(`${Math.round(seconds)}s`);

      return parts.join(' ') || '0s';
    } catch {
      return isoDuration;
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">Assessment Reports</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            select
            size="small"
            value={selectedSessionId}
            onChange={(e) => setSelectedSessionId(e.target.value)}
            sx={{ minWidth: 200 }}
            label="Filter by Session"
          >
            <MenuItem value="">All Sessions</MenuItem>
            {sessions.map((session) => (
              <MenuItem key={session.sessionId} value={session.sessionId}>
                {session.sessionName}
              </MenuItem>
            ))}
          </TextField>
          <Button
            startIcon={<Assessment />}
            variant="contained"
            onClick={() => setGenerateDialogOpen(true)}
          >
            Generate Assessment
          </Button>
          <IconButton onClick={loadAssessments}>
            <Refresh />
          </IconButton>
        </Box>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {loading && <LinearProgress sx={{ mb: 2 }} />}

      <TableContainer component={Paper} sx={{ mb: 3 }}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Session</TableCell>
              <TableCell>Operator</TableCell>
              <TableCell>Generated</TableCell>
              <TableCell>Score</TableCell>
              <TableCell>Grade</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {assessments.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  <Typography color="text.secondary">
                    No assessments found. Generate one to get started.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              assessments.map((assessment) => (
                <TableRow
                  key={assessment.assessmentId}
                  sx={{ cursor: 'pointer' }}
                  hover
                  onClick={() => setSelectedAssessment(assessment)}
                >
                  <TableCell>
                    {sessions.find(s => s.sessionId === assessment.sessionId)?.sessionName || assessment.sessionId}
                  </TableCell>
                  <TableCell>{assessment.operatorId}</TableCell>
                  <TableCell>{new Date(assessment.generatedAt).toLocaleString()}</TableCell>
                  <TableCell>{assessment.overallScore.toFixed(1)}%</TableCell>
                  <TableCell>
                    <Chip label={assessment.grade} color={getGradeColor(assessment.grade)} size="small" />
                  </TableCell>
                  <TableCell>
                    {assessment.passed ? (
                      <Chip icon={<CheckCircle />} label="Passed" color="success" size="small" />
                    ) : (
                      <Chip icon={<Cancel />} label="Failed" color="error" size="small" />
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <IconButton
                      size="small"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleViewMetrics(assessment.sessionId);
                      }}
                    >
                      <Timeline />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Detailed Assessment View */}
      {selectedAssessment && (
        <Paper sx={{ p: 3 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3 }}>
            <Typography variant="h6">Assessment Details</Typography>
            <Button onClick={() => setSelectedAssessment(null)}>Close</Button>
          </Box>

          <Grid container spacing={3} sx={{ mb: 3 }}>
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="text.secondary" variant="body2">Overall Score</Typography>
                  <Typography variant="h4">{selectedAssessment.overallScore.toFixed(1)}%</Typography>
                  <Chip
                    label={selectedAssessment.grade}
                    color={getGradeColor(selectedAssessment.grade)}
                    sx={{ mt: 1 }}
                  />
                </CardContent>
              </Card>
            </Grid>
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="text.secondary" variant="body2">Status</Typography>
                  <Box sx={{ mt: 1 }}>
                    {selectedAssessment.passed ? (
                      <Chip icon={<CheckCircle />} label="Passed" color="success" />
                    ) : (
                      <Chip icon={<Cancel />} label="Failed" color="error" />
                    )}
                  </Box>
                </CardContent>
              </Card>
            </Grid>
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="text.secondary" variant="body2">Operator</Typography>
                  <Typography variant="h6">{selectedAssessment.operatorId}</Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="text.secondary" variant="body2">Generated</Typography>
                  <Typography variant="body1">
                    {new Date(selectedAssessment.generatedAt).toLocaleString()}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {selectedAssessment.comments && (
            <Alert severity="info" sx={{ mb: 3 }}>
              {selectedAssessment.comments}
            </Alert>
          )}

          <Typography variant="h6" sx={{ mb: 2 }}>KPI Results</Typography>
          {selectedAssessment.kpiResults.map((kpi, index) => (
            <Accordion key={index}>
              <AccordionSummary expandIcon={<ExpandMore />}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, width: '100%' }}>
                  <Typography sx={{ flexGrow: 1 }}>{kpi.kpiName}</Typography>
                  <Chip
                    label={`${kpi.score.toFixed(1)}/${kpi.maxScore}`}
                    color={kpi.passed ? 'success' : 'error'}
                    size="small"
                  />
                  <Box sx={{ width: 200 }}>
                    <LinearProgress
                      variant="determinate"
                      value={(kpi.score / kpi.maxScore) * 100}
                      color={kpi.passed ? 'success' : 'error'}
                    />
                  </Box>
                </Box>
              </AccordionSummary>
              <AccordionDetails>
                <Typography variant="body2" color="text.secondary" paragraph>
                  {kpi.description}
                </Typography>
                <Grid container spacing={2}>
                  <Grid item xs={4}>
                    <Typography variant="caption" color="text.secondary">Actual Value</Typography>
                    <Typography variant="body1">{JSON.stringify(kpi.actualValue)}</Typography>
                  </Grid>
                  {kpi.targetValue !== undefined && (
                    <Grid item xs={4}>
                      <Typography variant="caption" color="text.secondary">Target Value</Typography>
                      <Typography variant="body1">{JSON.stringify(kpi.targetValue)}</Typography>
                    </Grid>
                  )}
                  {kpi.threshold !== undefined && (
                    <Grid item xs={4}>
                      <Typography variant="caption" color="text.secondary">Threshold</Typography>
                      <Typography variant="body1">{JSON.stringify(kpi.threshold)}</Typography>
                    </Grid>
                  )}
                  <Grid item xs={4}>
                    <Typography variant="caption" color="text.secondary">Weight</Typography>
                    <Typography variant="body1">{kpi.weight.toFixed(2)}</Typography>
                  </Grid>
                </Grid>
                {kpi.feedback && (
                  <Alert severity={kpi.passed ? 'success' : 'warning'} sx={{ mt: 2 }}>
                    {kpi.feedback}
                  </Alert>
                )}
              </AccordionDetails>
            </Accordion>
          ))}
        </Paper>
      )}

      {/* Generate Assessment Dialog */}
      <Dialog open={generateDialogOpen} onClose={() => setGenerateDialogOpen(false)}>
        <DialogTitle>Generate Assessment</DialogTitle>
        <DialogContent>
          <TextField
            select
            fullWidth
            margin="dense"
            label="Select Session"
            value={selectedSessionId}
            onChange={(e) => setSelectedSessionId(e.target.value)}
          >
            {sessions.map((session) => (
              <MenuItem key={session.sessionId} value={session.sessionId}>
                {session.sessionName}
              </MenuItem>
            ))}
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setGenerateDialogOpen(false)}>Cancel</Button>
          <Button
            onClick={handleGenerateAssessment}
            variant="contained"
            disabled={!selectedSessionId || loading}
          >
            Generate
          </Button>
        </DialogActions>
      </Dialog>

      {/* Metrics Dialog */}
      <Dialog
        open={metricsDialogOpen}
        onClose={() => setMetricsDialogOpen(false)}
        maxWidth="md"
        fullWidth
      >
        <DialogTitle>Performance Metrics</DialogTitle>
        <DialogContent>
          {selectedMetrics && (
            <Grid container spacing={2} sx={{ mt: 1 }}>
              <Grid item xs={6}>
                <Typography variant="caption" color="text.secondary">Session Duration</Typography>
                <Typography variant="h6">{formatDuration(selectedMetrics.sessionDuration)}</Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="caption" color="text.secondary">Total Actions</Typography>
                <Typography variant="h6">{selectedMetrics.totalOperatorActions}</Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="caption" color="text.secondary">Tag Writes</Typography>
                <Typography variant="h6">{selectedMetrics.tagWrites}</Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="caption" color="text.secondary">Events Processed</Typography>
                <Typography variant="h6">{selectedMetrics.eventsProcessed}</Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="caption" color="text.secondary">Snapshots Created</Typography>
                <Typography variant="h6">{selectedMetrics.snapshotsCreated}</Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="caption" color="text.secondary">Snapshots Restored</Typography>
                <Typography variant="h6">{selectedMetrics.snapshotsRestored}</Typography>
              </Grid>
              <Grid item xs={12}>
                <Typography variant="subtitle2" sx={{ mt: 2, mb: 1 }}>Event Breakdown</Typography>
                {Object.entries(selectedMetrics.eventTypeBreakdown).map(([type, count]) => (
                  <Box key={type} sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                    <Typography variant="body2">{type}</Typography>
                    <Typography variant="body2" fontWeight="bold">{count}</Typography>
                  </Box>
                ))}
              </Grid>
            </Grid>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setMetricsDialogOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default AssessmentViewer;
