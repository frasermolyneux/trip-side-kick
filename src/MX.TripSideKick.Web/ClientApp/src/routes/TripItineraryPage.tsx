import { useMemo, useState } from 'react';
import { useParams, Link as RouterLink } from 'react-router';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Divider,
  FormControl,
  FormControlLabel,
  IconButton,
  InputLabel,
  List,
  ListItem,
  ListItemText,
  MenuItem,
  Radio,
  RadioGroup,
  Select,
  Stack,
  TextField,
  Typography
} from '@mui/material';

import { useAuth } from '../auth/AuthContext';
import { MembershipRole } from '../api/roles';
import type { ItineraryItemResponse } from '../api/types';
import { useMembers } from '../queries/members';
import { useTravellers } from '../queries/travellers';
import { useTrip } from '../queries/trips';
import {
  useAddItineraryComment,
  useCreateItineraryItem,
  useDeleteItineraryItem,
  useItineraryComments,
  useItineraryItems,
  useScheduleItineraryItem,
  useSetItineraryApplicability,
  useTravellerFilter,
  useTripActivityFeed,
  useUnscheduleItineraryItem,
  useUpdateItineraryItemContent,
  useUpdateTravellerFilter
} from '../queries/itinerary';

/** Journey 5: itinerary + collaborative planning surface. */
export function TripItineraryPage() {
  const { tripId = '' } = useParams();
  const { auth } = useAuth();
  const { data: trip, isLoading: tripLoading } = useTrip(tripId);
  const { data: members } = useMembers(tripId);
  const { data: travellers } = useTravellers(tripId);
  const { data: items, isLoading: itemsLoading } = useItineraryItems(tripId);
  const { data: feed } = useTripActivityFeed(tripId);
  const { data: filter } = useTravellerFilter(tripId);

  const createItem = useCreateItineraryItem(tripId);
  const updateContent = useUpdateItineraryItemContent(tripId);
  const scheduleItem = useScheduleItineraryItem(tripId);
  const unscheduleItem = useUnscheduleItineraryItem(tripId);
  const setApplicability = useSetItineraryApplicability(tripId);
  const deleteItem = useDeleteItineraryItem(tripId);
  const updateFilter = useUpdateTravellerFilter(tripId);

  const [title, setTitle] = useState('');
  const [notes, setNotes] = useState('');
  const [location, setLocation] = useState('');
  const [applicability, setApplicabilityDraft] = useState<string[]>([]);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [scheduleDate, setScheduleDate] = useState('');
  const [error, setError] = useState<string | null>(null);

  const myMembership = members?.find((member) => member.subjectId === auth.subjectId);
  const canEdit =
    myMembership?.role === MembershipRole.Owner || myMembership?.role === MembershipRole.Editor;
  const canComment = Boolean(myMembership); // Any member can comment.

  const dayByDayEnabled = trip?.dates.status === 'confirmed';
  const scheduledItems = useMemo(
    () => (items ?? []).filter((item) => item.schedule.status === 'scheduled'),
    [items]
  );
  const ideas = useMemo(
    () => (items ?? []).filter((item) => item.schedule.status === 'unscheduled'),
    [items]
  );

  if (tripLoading) {
    return <CircularProgress aria-label="Loading trip" />;
  }
  if (!trip) {
    return <Typography role="alert">We could not load this trip.</Typography>;
  }

  async function handleCreate() {
    setError(null);
    try {
      await createItem.mutateAsync({
        title,
        notes: notes || null,
        location: location || null,
        applicableTravellerIds: applicability
      });
      setTitle('');
      setNotes('');
      setLocation('');
      setApplicabilityDraft([]);
    } catch {
      setError('Could not create idea. Only Owners/Editors can add itinerary items.');
    }
  }

  async function handleSchedule(item: ItineraryItemResponse) {
    if (!scheduleDate) {
      setError('Pick a date first.');
      return;
    }
    setError(null);
    try {
      await scheduleItem.mutateAsync({
        itemId: item.id,
        eTag: item.eTag,
        body: { date: scheduleDate, startTime: null, endTime: null }
      });
    } catch {
      setError('Could not schedule. Trip dates must be confirmed, and the date must fall inside the trip window.');
    }
  }

  async function handleUnschedule(item: ItineraryItemResponse) {
    setError(null);
    try {
      await unscheduleItem.mutateAsync({ itemId: item.id, eTag: item.eTag });
    } catch {
      setError('Could not demote back to idea.');
    }
  }

  async function handleDelete(item: ItineraryItemResponse) {
    setError(null);
    try {
      await deleteItem.mutateAsync(item.id);
    } catch {
      setError('Could not delete item.');
    }
  }

  async function handleApplicabilityChange(item: ItineraryItemResponse, travellerIds: string[]) {
    setError(null);
    try {
      await setApplicability.mutateAsync({
        itemId: item.id,
        eTag: item.eTag,
        body: { travellerIds }
      });
    } catch {
      setError('Could not update applicability.');
    }
  }

  async function handleFilterChange(mode: 'everyone' | 'me' | 'selected', selected: string[]) {
    if (!filter) return;
    setError(null);
    try {
      await updateFilter.mutateAsync({
        eTag: filter.eTag,
        body: { mode, selectedTravellerIds: mode === 'selected' ? selected : [] }
      });
    } catch {
      setError('Could not update filter.');
    }
  }

  return (
    <Box data-testid="trip-itinerary-page">
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 2 }}>
        <Typography variant="h4" component="h2">
          Itinerary — {trip.name}
        </Typography>
        <Button component={RouterLink} to={`/trips/${tripId}`} size="small">
          Back to dashboard
        </Button>
      </Stack>

      {!dayByDayEnabled && (
        <Alert severity="info" sx={{ mb: 2 }} data-testid="dates-not-confirmed-banner">
          Trip dates are {trip.dates.status}. Ideas can be captured now; day-by-day scheduling
          unlocks once dates are confirmed.
        </Alert>
      )}
      {error && <Alert severity="error" sx={{ mb: 2 }} data-testid="itinerary-error">{error}</Alert>}

      {/* Filter */}
      <Box sx={{ mb: 3 }} data-testid="traveller-filter">
        <Typography variant="h6" component="h3">Who am I planning for?</Typography>
        {filter ? (
          <RadioGroup
            row
            value={filter.mode}
            onChange={(e) => void handleFilterChange(e.target.value as 'everyone' | 'me' | 'selected', filter.selectedTravellerIds)}
          >
            <FormControlLabel value="everyone" control={<Radio data-testid="filter-everyone" />} label="Everyone" />
            <FormControlLabel value="me" control={<Radio data-testid="filter-me" />} label="Just me" />
            <FormControlLabel value="selected" control={<Radio data-testid="filter-selected" />} label="Selected travellers" />
          </RadioGroup>
        ) : (
          <CircularProgress size={20} />
        )}
        {filter?.mode === 'selected' && (
          <FormControl fullWidth sx={{ mt: 1 }}>
            <InputLabel>Travellers</InputLabel>
            <Select
              multiple
              value={filter.selectedTravellerIds}
              label="Travellers"
              onChange={(e) => void handleFilterChange('selected', e.target.value as string[])}
              renderValue={(selected) =>
                (selected as string[])
                  .map((id) => travellers?.find((t) => t.id === id)?.displayName ?? id)
                  .join(', ')
              }
              inputProps={{ 'data-testid': 'filter-selected-picker' } as never}
            >
              {(travellers ?? []).map((t) => (
                <MenuItem key={t.id} value={t.id}>
                  <Checkbox checked={filter.selectedTravellerIds.includes(t.id)} />
                  <ListItemText primary={t.displayName} />
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}
      </Box>

      <Divider sx={{ my: 2 }} />

      {/* Create idea */}
      {canEdit && (
        <Box sx={{ mb: 3 }} data-testid="create-idea-form">
          <Typography variant="h6" component="h3">Add an idea</Typography>
          <Stack spacing={1} sx={{ maxWidth: 500 }}>
            <TextField
              label="Title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              slotProps={{ htmlInput: { 'data-testid': 'idea-title-input' } }}
            />
            <TextField
              label="Notes"
              value={notes}
              multiline
              onChange={(e) => setNotes(e.target.value)}
              slotProps={{ htmlInput: { 'data-testid': 'idea-notes-input' } }}
            />
            <TextField
              label="Location"
              value={location}
              onChange={(e) => setLocation(e.target.value)}
              slotProps={{ htmlInput: { 'data-testid': 'idea-location-input' } }}
            />
            <FormControl fullWidth>
              <InputLabel>Applies to (empty = everyone)</InputLabel>
              <Select
                multiple
                value={applicability}
                label="Applies to (empty = everyone)"
                onChange={(e) => setApplicabilityDraft(e.target.value as string[])}
                renderValue={(selected) =>
                  (selected as string[]).length === 0
                    ? 'Everyone'
                    : (selected as string[])
                        .map((id) => travellers?.find((t) => t.id === id)?.displayName ?? id)
                        .join(', ')
                }
                inputProps={{ 'data-testid': 'idea-applicability-picker' } as never}
              >
                {(travellers ?? []).map((t) => (
                  <MenuItem key={t.id} value={t.id}>
                    <Checkbox checked={applicability.includes(t.id)} />
                    <ListItemText primary={t.displayName} />
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Button
              variant="contained"
              onClick={() => void handleCreate()}
              disabled={!title.trim()}
              data-testid="create-idea-submit"
            >
              Add idea
            </Button>
          </Stack>
        </Box>
      )}

      {/* Ideas */}
      <Typography variant="h6" component="h3">Ideas</Typography>
      {itemsLoading ? (
        <CircularProgress />
      ) : (
        <List data-testid="ideas-list">
          {ideas.length === 0 && <ListItem><ListItemText primary="No ideas yet." /></ListItem>}
          {ideas.map((item) => (
            <ItineraryItemRow
              key={item.id}
              item={item}
              travellerNames={new Map((travellers ?? []).map((t) => [t.id, t.displayName]))}
              canEdit={canEdit}
              canComment={canComment}
              dayByDayEnabled={dayByDayEnabled}
              scheduleDate={scheduleDate}
              onScheduleDateChange={setScheduleDate}
              onSchedule={() => void handleSchedule(item)}
              onUnschedule={() => void handleUnschedule(item)}
              onDelete={() => void handleDelete(item)}
              onApplicabilityChange={(ids) => void handleApplicabilityChange(item, ids)}
              onToggleComments={() => setSelectedItemId(item.id === selectedItemId ? null : item.id)}
              selected={selectedItemId === item.id}
              tripId={tripId}
              travellers={travellers ?? []}
              updateContent={updateContent}
            />
          ))}
        </List>
      )}

      {/* Scheduled */}
      {dayByDayEnabled && (
        <>
          <Typography variant="h6" component="h3" sx={{ mt: 2 }}>Day-by-day</Typography>
          <List data-testid="scheduled-list">
            {scheduledItems.length === 0 && <ListItem><ListItemText primary="Nothing scheduled yet." /></ListItem>}
            {scheduledItems
              .slice()
              .sort((a, b) => (a.schedule.date ?? '').localeCompare(b.schedule.date ?? ''))
              .map((item) => (
                <ItineraryItemRow
                  key={item.id}
                  item={item}
                  travellerNames={new Map((travellers ?? []).map((t) => [t.id, t.displayName]))}
                  canEdit={canEdit}
                  canComment={canComment}
                  dayByDayEnabled={dayByDayEnabled}
                  scheduleDate={scheduleDate}
                  onScheduleDateChange={setScheduleDate}
                  onSchedule={() => void handleSchedule(item)}
                  onUnschedule={() => void handleUnschedule(item)}
                  onDelete={() => void handleDelete(item)}
                  onApplicabilityChange={(ids) => void handleApplicabilityChange(item, ids)}
                  onToggleComments={() => setSelectedItemId(item.id === selectedItemId ? null : item.id)}
                  selected={selectedItemId === item.id}
                  tripId={tripId}
                  travellers={travellers ?? []}
                  updateContent={updateContent}
                />
              ))}
          </List>
        </>
      )}

      {/* Feed */}
      <Divider sx={{ my: 2 }} />
      <Typography variant="h6" component="h3">Activity feed</Typography>
      <List data-testid="activity-feed">
        {(feed ?? []).map((entry) => (
          <ListItem key={entry.id}>
            <ListItemText
              primary={entry.summary}
              secondary={`${entry.actorDisplayName} · ${new Date(entry.occurredAt).toLocaleString()}`}
            />
          </ListItem>
        ))}
        {feed && feed.length === 0 && <ListItem><ListItemText primary="No activity yet." /></ListItem>}
      </List>
    </Box>
  );
}

interface ItineraryItemRowProps {
  item: ItineraryItemResponse;
  travellerNames: Map<string, string>;
  canEdit: boolean;
  canComment: boolean;
  dayByDayEnabled: boolean;
  scheduleDate: string;
  onScheduleDateChange: (value: string) => void;
  onSchedule: () => void;
  onUnschedule: () => void;
  onDelete: () => void;
  onApplicabilityChange: (ids: string[]) => void;
  onToggleComments: () => void;
  selected: boolean;
  tripId: string;
  travellers: { id: string; displayName: string }[];
  updateContent: ReturnType<typeof useUpdateItineraryItemContent>;
}

function ItineraryItemRow(props: ItineraryItemRowProps) {
  const {
    item, travellerNames, canEdit, canComment, dayByDayEnabled, scheduleDate,
    onScheduleDateChange, onSchedule, onUnschedule, onDelete, onApplicabilityChange,
    onToggleComments, selected, tripId, travellers, updateContent
  } = props;

  const { data: comments } = useItineraryComments(tripId, selected ? item.id : undefined);
  const addComment = useAddItineraryComment(tripId);
  const [commentDraft, setCommentDraft] = useState('');
  const [editing, setEditing] = useState(false);
  const [titleDraft, setTitleDraft] = useState(item.title);

  async function submitComment() {
    if (!commentDraft.trim()) return;
    try {
      await addComment.mutateAsync({ itemId: item.id, body: { body: commentDraft } });
      setCommentDraft('');
    } catch {
      // handled by parent-level error state
    }
  }

  async function saveTitle() {
    try {
      await updateContent.mutateAsync({
        itemId: item.id,
        eTag: item.eTag,
        body: { title: titleDraft, notes: item.notes, location: item.location }
      });
      setEditing(false);
    } catch {
      // ignore
    }
  }

  const applicabilityLabel = item.applicableTravellerIds.length === 0
    ? 'Everyone'
    : item.applicableTravellerIds.map((id) => travellerNames.get(id) ?? id).join(', ');

  return (
    <ListItem
      alignItems="flex-start"
      data-testid={`itinerary-item-${item.id}`}
      sx={{ flexDirection: 'column', alignItems: 'stretch', border: '1px solid #eee', mb: 1, borderRadius: 1 }}
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        {editing ? (
          <>
            <TextField size="small" value={titleDraft} onChange={(e) => setTitleDraft(e.target.value)} />
            <Button size="small" onClick={() => void saveTitle()}>Save</Button>
            <Button size="small" onClick={() => setEditing(false)}>Cancel</Button>
          </>
        ) : (
          <Typography variant="subtitle1" data-testid="itinerary-item-title">{item.title}</Typography>
        )}
        <Chip
          size="small"
          label={item.schedule.status === 'scheduled' ? `Scheduled ${item.schedule.date}` : 'Idea'}
          color={item.schedule.status === 'scheduled' ? 'success' : 'default'}
        />
        <Chip size="small" label={applicabilityLabel} />
      </Stack>
      {item.notes && <Typography variant="body2" color="text.secondary">{item.notes}</Typography>}
      {item.location && <Typography variant="caption">📍 {item.location}</Typography>}

      {canEdit && (
        <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: 'wrap' }}>
          <Button size="small" onClick={() => { setTitleDraft(item.title); setEditing(true); }} data-testid="edit-item">
            Edit
          </Button>
          {item.schedule.status === 'unscheduled' && dayByDayEnabled && (
            <>
              <TextField
                size="small"
                type="date"
                value={scheduleDate}
                onChange={(e) => onScheduleDateChange(e.target.value)}
                slotProps={{ htmlInput: { 'data-testid': 'schedule-date-input' } }}
              />
              <Button size="small" onClick={onSchedule} data-testid="schedule-item">Schedule</Button>
            </>
          )}
          {item.schedule.status === 'scheduled' && (
            <Button size="small" onClick={onUnschedule} data-testid="unschedule-item">Back to idea</Button>
          )}
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel>Applies to</InputLabel>
            <Select
              multiple
              value={item.applicableTravellerIds}
              label="Applies to"
              onChange={(e) => onApplicabilityChange(e.target.value as string[])}
              renderValue={() => applicabilityLabel}
              inputProps={{ 'data-testid': `applicability-${item.id}` } as never}
            >
              {travellers.map((t) => (
                <MenuItem key={t.id} value={t.id}>
                  <Checkbox checked={item.applicableTravellerIds.includes(t.id)} />
                  <ListItemText primary={t.displayName} />
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <IconButton size="small" color="error" onClick={onDelete} data-testid="delete-item" aria-label="Delete item">
            🗑
          </IconButton>
        </Stack>
      )}

      <Button size="small" onClick={onToggleComments} data-testid="toggle-comments">
        {selected ? 'Hide comments' : 'Show comments'}
      </Button>

      {selected && (
        <Box sx={{ mt: 1 }} data-testid="comments-panel">
          <List dense>
            {(comments ?? []).map((c) => (
              <ListItem key={c.id}>
                <ListItemText
                  primary={c.body}
                  secondary={`${c.authorDisplayName} · ${new Date(c.createdAt).toLocaleString()}`}
                />
              </ListItem>
            ))}
            {comments && comments.length === 0 && (
              <ListItem><ListItemText primary="No comments yet." /></ListItem>
            )}
          </List>
          {canComment && (
            <Stack direction="row" spacing={1}>
              <TextField
                size="small"
                value={commentDraft}
                onChange={(e) => setCommentDraft(e.target.value)}
                placeholder="Add a comment"
                fullWidth
                slotProps={{ htmlInput: { 'data-testid': 'comment-input' } }}
              />
              <Button
                size="small"
                variant="contained"
                onClick={() => void submitComment()}
                data-testid="submit-comment"
              >
                Comment
              </Button>
            </Stack>
          )}
        </Box>
      )}
    </ListItem>
  );
}

export default TripItineraryPage;
