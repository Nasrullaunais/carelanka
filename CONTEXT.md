# CareLanka Domain Language

CareLanka coordinates hospital work across Emergency, Staff, Equipment and Patient
Management. These terms keep the Emergency response conversation consistent across those
components.

## Emergency response

**Emergency Call**:
A request for emergency assistance, including the caller's report and the scene location.
_Avoid_: Incident report, ticket

**Caller**:
The authenticated person who reports an emergency; the caller may or may not be the patient.
_Avoid_: Customer

**Patient**:
The person who needs assistance, whether or not their identity is known when the call is made.
_Avoid_: Customer, caller when the distinction matters

**Ambulance**:
The response vehicle to which a current crew may be assigned.
_Avoid_: Paramedic when referring to the dispatched resource

**Current Ambulance Crew**:
The on-duty crew members presently responsible for an ambulance and considered when deciding
whether it can respond.
_Avoid_: Active paramedics

**Responding Crew**:
The permanent record of the crew members who attended a particular dispatch.
_Avoid_: Current crew when describing a past dispatch

**Eligible Ambulance**:
An active and serviceable ambulance with enough current crew, no live dispatch, and a usable
recent location.
_Avoid_: Available paramedic

**Dispatch Proposal**:
An explained recommendation for responding to an emergency call that has not yet moved an
ambulance.
_Avoid_: Dispatch

**Dispatch**:
One confirmed ambulance response to one emergency call, from assignment through handover or
another terminal outcome.
_Avoid_: Job, proposal

**Acknowledgement**:
The assigned crew's confirmation that they have received responsibility for a dispatch.
_Avoid_: Pickup, acceptance when referring to reaching the patient

**Cancellation Request**:
A caller's request, after dispatch assignment, for a Duty Manager to review whether the
response should be cancelled; creating it does not recall the ambulance.
_Avoid_: Cancellation when the request is still pending

**At Scene**:
The operational milestone at which the crew has reached the patient.
_Avoid_: Treating

**Handover**:
The point at which hospital staff accept responsibility for the transported patient and the
dispatch can complete.
_Avoid_: Drop-off, arrived
